namespace InternLink.Web.Services.Jobs;

public class JobIngestionBackgroundService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(12);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobIngestionBackgroundService> _logger;

    public JobIngestionBackgroundService(
        IServiceScopeFactory scopeFactory, 
        ILogger<JobIngestionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job Ingestion Background Service started. Waiting {Seconds}s before first sync.", InitialDelay.TotalSeconds);

        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Job Ingestion Background Service executing scheduled sync...");

                using var scope = _scopeFactory.CreateScope();
                var ingestionService = scope.ServiceProvider.GetRequiredService<IExternalJobIngestionService>();
                var result = await ingestionService.SyncAllSourcesAsync(stoppingToken);

                _logger.LogInformation(
                    "Job Ingestion Background Service finished: {Ingested} new, {Skipped} skipped, {Failed} failed.",
                    result.Ingested, result.Skipped, result.Failed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred during automated external job sync.");
            }

            try
            {
                await Task.Delay(SyncInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Job Ingestion Background Service stopped.");
    }
}
