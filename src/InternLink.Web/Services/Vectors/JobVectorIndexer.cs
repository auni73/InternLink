using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.AI;

namespace InternLink.Web.Services.Vectors;

/// <summary>
/// Drains the job index queue off the request path: embedding calls never block a user request.
/// </summary>
public class JobVectorIndexer : BackgroundService
{
    private const int MaxAttempts = 3;

    private readonly IJobIndexQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobVectorIndexer> _logger;

    public JobVectorIndexer(
        IJobIndexQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<JobVectorIndexer> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureCollectionAsync(stoppingToken);

        await foreach (var command in _queue.ReadAllAsync(stoppingToken))
        {
            await ProcessWithRetryAsync(command, stoppingToken);
        }
    }

    private async Task EnsureCollectionAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IJobVectorStore>();
            await store.EnsureCollectionAsync(ct);
        }
        catch (Exception ex)
        {
            // The app must still boot and serve FTS-backed search when Qdrant is down.
            _logger.LogWarning(ex, "Could not ensure the Qdrant collection at startup. Semantic search will be unavailable until it recovers.");
        }
    }

    private async Task ProcessWithRetryAsync(JobIndexCommand command, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await ProcessAsync(command, ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                if (attempt == MaxAttempts)
                {
                    _logger.LogError(
                        ex,
                        "Dropping {Operation} for job {JobId} after {Attempts} attempts. Run Admin ReindexAll to reconcile.",
                        command.Operation,
                        command.JobId,
                        MaxAttempts);
                    return;
                }

                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                _logger.LogWarning(
                    ex,
                    "{Operation} for job {JobId} failed (attempt {Attempt}/{Max}); retrying in {Delay}s.",
                    command.Operation,
                    command.JobId,
                    attempt,
                    MaxAttempts,
                    delay.TotalSeconds);

                await Task.Delay(delay, ct);
            }
        }
    }

    private async Task ProcessAsync(JobIndexCommand command, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IJobVectorStore>();

        if (command.Operation == JobIndexOperation.Delete)
        {
            await store.DeleteJobAsync(command.JobId, ct);
            _logger.LogInformation("Removed job {JobId} from the vector index.", command.JobId);
            return;
        }

        var jobs = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var source = await jobs.GetJobVectorSourceAsync(command.JobId, ct);
        if (source is null)
        {
            _logger.LogWarning("Job {JobId} no longer exists; skipping index upsert.", command.JobId);
            return;
        }

        var embedder = scope.ServiceProvider.GetRequiredService<IEmbeddingClient>();
        var vector = await embedder.EmbedAsync(source.ToDocumentText(), EmbeddingPurpose.Document, ct);

        await store.UpsertJobAsync(source.JobId, vector, source.ToPayload(), ct);
        _logger.LogInformation("Indexed job {JobId} into the vector index.", source.JobId);
    }
}
