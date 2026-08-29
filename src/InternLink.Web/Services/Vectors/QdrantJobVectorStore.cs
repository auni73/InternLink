using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using InternLink.Web.Services.AI;

namespace InternLink.Web.Services.Vectors;

public class QdrantJobVectorStore : IJobVectorStore, IVectorSearch, IDisposable
{
    public const ulong VectorSize = 768;
    private const string DeadlineField = "deadlineUnix";

    private readonly QdrantClient? _client;
    private readonly string _collection;
    private readonly ILogger<QdrantJobVectorStore> _logger;

    public QdrantJobVectorStore(IOptions<QdrantOptions> options, ILogger<QdrantJobVectorStore> logger)
    {
        var settings = options.Value;
        _collection = settings.CollectionName;
        _logger = logger;
        _client = TryCreateClient(settings, logger);
    }

    public bool IsConfigured => _client is not null;

    public async Task EnsureCollectionAsync(CancellationToken ct = default)
    {
        var client = Require();

        if (await client.CollectionExistsAsync(_collection, ct))
        {
            return;
        }

        await client.CreateCollectionAsync(
            _collection,
            new VectorParams { Size = VectorSize, Distance = Distance.Cosine },
            cancellationToken: ct);

        // Every search filters on this field, so index it rather than scanning payloads.
        await client.CreatePayloadIndexAsync(
            _collection,
            DeadlineField,
            PayloadSchemaType.Integer,
            cancellationToken: ct);

        _logger.LogInformation(
            "Created Qdrant collection {Collection} ({Size}-d, cosine).",
            _collection,
            VectorSize);
    }

    public async Task UpsertJobAsync(Guid jobId, float[] vector, JobVectorPayload payload, CancellationToken ct = default)
    {
        var client = Require();

        if (vector.Length != (int)VectorSize)
        {
            throw new SemanticSearchUnavailableException(
                $"Expected a {VectorSize}-dimension vector but received {vector.Length}.");
        }

        var point = new PointStruct
        {
            Id = jobId,
            Vectors = vector,
            Payload =
            {
                ["companyId"] = payload.CompanyId.ToString(),
                ["locationType"] = payload.LocationType,
                [DeadlineField] = payload.DeadlineUnix
            }
        };

        var skillIds = new ListValue();
        foreach (var skillId in payload.SkillIds)
        {
            skillIds.Values.Add(new Value { StringValue = skillId.ToString() });
        }
        point.Payload["skillIds"] = new Value { ListValue = skillIds };

        try
        {
            await client.UpsertAsync(_collection, [point], cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new SemanticSearchUnavailableException($"Failed to upsert job {jobId} into the vector index.", ex);
        }
    }

    public async Task DeleteJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var client = Require();

        try
        {
            await client.DeleteAsync(_collection, jobId, cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new SemanticSearchUnavailableException($"Failed to delete job {jobId} from the vector index.", ex);
        }
    }

    public async Task<IReadOnlyList<(Guid JobId, float Score)>> SearchJobsAsync(
        float[] queryVector,
        int topK,
        CancellationToken ct = default)
    {
        var client = Require();

        // Expired postings must never surface, even if the delete pass lagged.
        var filter = new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = DeadlineField,
                        Range = new Qdrant.Client.Grpc.Range { Gte = DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
                    }
                }
            }
        };

        try
        {
            var results = await client.QueryAsync(
                _collection,
                query: queryVector,
                filter: filter,
                limit: (ulong)Math.Max(1, topK),
                cancellationToken: ct);

            return results
                .Where(p => Guid.TryParse(p.Id?.Uuid, out _))
                .Select(p => (Guid.Parse(p.Id.Uuid), p.Score))
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new SemanticSearchUnavailableException("The vector search backend is unavailable.", ex);
        }
    }

    private QdrantClient Require() =>
        _client ?? throw new SemanticSearchUnavailableException("Qdrant is not configured on this environment.");

    private static QdrantClient? TryCreateClient(QdrantOptions settings, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(settings.Endpoint) ||
            string.IsNullOrWhiteSpace(settings.ApiKey) ||
            settings.Endpoint.StartsWith("https://your-", StringComparison.OrdinalIgnoreCase) ||
            settings.ApiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Qdrant is not configured. Semantic search will be unavailable; set Qdrant:* via user-secrets.");
            return null;
        }

        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var uri))
        {
            logger.LogWarning("Qdrant:Endpoint '{Endpoint}' is not a valid absolute URL. Semantic search will be unavailable.", settings.Endpoint);
            return null;
        }

        // Constructing the client does not open a connection, so a bad host cannot block startup.
        return new QdrantClient(uri.Host, settings.GrpcPort, https: true, apiKey: settings.ApiKey);
    }

    public void Dispose() => _client?.Dispose();
}
