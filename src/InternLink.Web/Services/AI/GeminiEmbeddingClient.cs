using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;

namespace InternLink.Web.Services.AI;

public class GeminiEmbeddingClient : IEmbeddingClient
{
    private const string ApiKeyHeader = "x-goog-api-key";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly IGeminiKeyPool _keyPool;
    private readonly GeminiOptions _options;
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;
    private readonly ILogger<GeminiEmbeddingClient> _logger;

    public GeminiEmbeddingClient(
        HttpClient http,
        IGeminiKeyPool keyPool,
        IOptions<GeminiOptions> options,
        ILogger<GeminiEmbeddingClient> logger)
    {
        _http = http;
        _keyPool = keyPool;
        _options = options.Value;
        _logger = logger;

        _pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .HandleResult(r => (int)r.StatusCode >= 500),
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(Math.Max(0, _options.RetryBaseDelayMilliseconds)),
                OnRetry = args =>
                {
                    args.Outcome.Result?.Dispose();
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)))
            .Build();
    }

    public async Task<float[]> EmbedAsync(string text, EmbeddingPurpose purpose, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new SemanticSearchUnavailableException("Cannot embed empty text.");
        }

        if (_keyPool.KeyCount == 0)
        {
            throw new SemanticSearchUnavailableException("Embeddings are not configured on this environment.");
        }

        var modelPath = $"models/{_options.EmbeddingModel}";
        var payload = JsonSerializer.Serialize(
            new EmbedContentRequest
            {
                Model = modelPath,
                Content = new GeminiContent { Parts = [new GeminiPart { Text = text }] },
                TaskType = purpose == EmbeddingPurpose.Document ? "RETRIEVAL_DOCUMENT" : "RETRIEVAL_QUERY",
                OutputDimensionality = GeminiEmbeddingDimensions
            },
            SerializerOptions);

        var endpoint = $"v1beta/{modelPath}:embedContent";

        // Embeddings share the generation quota pool, so they share its rotation and cooldowns.
        for (var attempt = 0; attempt < _keyPool.KeyCount; attempt++)
        {
            if (!_keyPool.TryLease(out var lease))
            {
                break;
            }

            HttpResponseMessage response;
            try
            {
                response = await _pipeline.ExecuteAsync(
                    async token => await SendAsync(endpoint, payload, lease.ApiKey, token),
                    ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Embedding call failed irrecoverably.");
                throw new SemanticSearchUnavailableException("The embedding service is unavailable.", ex);
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode && IsKeyScopedFailure(response.StatusCode, body))
                {
                    _keyPool.ReportKeyFailure(lease.Index);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Embedding request returned {StatusCode}.", (int)response.StatusCode);
                    throw new SemanticSearchUnavailableException("The embedding service rejected the request.");
                }

                return ParseEmbedding(body);
            }
        }

        _logger.LogWarning("All Gemini keys are cooling down; embedding request rejected.");
        throw new SemanticSearchUnavailableException("Embedding capacity is temporarily exhausted.");
    }

    public const int GeminiEmbeddingDimensions = 768;

    private async Task<HttpResponseMessage> SendAsync(
        string endpoint,
        string payload,
        string apiKey,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation(ApiKeyHeader, apiKey);

        return await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
    }

    private static float[] ParseEmbedding(string body)
    {
        EmbedContentResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<EmbedContentResponse>(body, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new SemanticSearchUnavailableException("The embedding response was unreadable.", ex);
        }

        var values = parsed?.Embedding?.Values;
        if (values is null || values.Length == 0)
        {
            throw new SemanticSearchUnavailableException("The embedding response contained no vector.");
        }

        if (values.Length != GeminiEmbeddingDimensions)
        {
            throw new SemanticSearchUnavailableException(
                $"Expected a {GeminiEmbeddingDimensions}-dimension embedding but received {values.Length}.");
        }

        return values;
    }

    private static bool IsKeyScopedFailure(HttpStatusCode status, string body)
    {
        if (status is HttpStatusCode.TooManyRequests or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return true;
        }

        return body.Contains("API_KEY_INVALID", StringComparison.OrdinalIgnoreCase)
            || body.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
            || body.Contains("quota", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class EmbedContentRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public GeminiContent Content { get; set; } = new();

    [JsonPropertyName("taskType")]
    public string TaskType { get; set; } = string.Empty;

    [JsonPropertyName("outputDimensionality")]
    public int OutputDimensionality { get; set; }
}

internal sealed class EmbedContentResponse
{
    [JsonPropertyName("embedding")]
    public EmbeddingValues? Embedding { get; set; }
}

internal sealed class EmbeddingValues
{
    [JsonPropertyName("values")]
    public float[]? Values { get; set; }
}
