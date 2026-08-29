using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;

namespace InternLink.Web.Services.AI;

public class GeminiClient : IGeminiClient
{
    private const string ApiKeyHeader = "x-goog-api-key";
    private const string BusyMessage = "AI features are temporarily busy. Please try again in a minute.";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly IGeminiKeyPool _keyPool;
    private readonly IAIHistoryRepository _ledger;
    private readonly GeminiOptions _options;
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;
    private readonly ILogger<GeminiClient> _logger;

    public GeminiClient(
        HttpClient http,
        IGeminiKeyPool keyPool,
        IAIHistoryRepository ledger,
        IOptions<GeminiOptions> options,
        ILogger<GeminiClient> logger)
    {
        _http = http;
        _keyPool = keyPool;
        _ledger = ledger;
        _options = options.Value;
        _logger = logger;

        _pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
            {
                // 429 is deliberately excluded: it rotates keys instead of hammering the same one.
                // 400 is excluded too — malformed input will not fix itself.
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
                    _logger.LogWarning(
                        "Gemini call failed (attempt {Attempt}); retrying after backoff.",
                        args.AttemptNumber + 1);
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)))
            .Build();
    }

    public async Task<GeminiResponse> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        IntegrationFeature feature,
        Guid userId,
        bool jsonMode,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            throw new AiServiceException("The AI request was empty.");
        }

        if (_keyPool.KeyCount == 0)
        {
            throw new AiServiceException("AI features are not configured on this environment.");
        }

        var payload = BuildRequestJson(systemPrompt, userPrompt, jsonMode);
        var endpoint = $"v1beta/models/{_options.Model}:generateContent";

        // One pass over the pool: a quota-blocked key rotates to the next rather than retrying in place.
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
                _logger.LogError(ex, "Gemini call failed irrecoverably for feature {Feature}.", feature);
                throw new AiServiceException("The AI service is currently unavailable. Please try again.", ex);
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(ct);

                // Only inspect the body on failures: a successful generation may legitimately contain the word "quota".
                if (!response.IsSuccessStatusCode && IsKeyScopedFailure(response.StatusCode, body))
                {
                    _keyPool.ReportKeyFailure(lease.Index);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    _logger.LogError("Gemini rejected the request for feature {Feature} as malformed.", feature);
                    throw new AiServiceException("The AI request was rejected as invalid.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Gemini returned {StatusCode} for feature {Feature}.",
                        (int)response.StatusCode,
                        feature);
                    throw new AiServiceException("The AI service is currently unavailable. Please try again.");
                }

                var result = ParseResponse(body);
                await RecordLedgerAsync(systemPrompt, feature, userId, result, ct);
                return result;
            }
        }

        _logger.LogWarning("All Gemini keys are cooling down; rejecting {Feature} request.", feature);
        throw new AiServiceException(BusyMessage);
    }

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
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
    }

    private static string BuildRequestJson(string systemPrompt, string userPrompt, bool jsonMode)
    {
        var request = new GeminiRequest
        {
            Contents =
            [
                new GeminiContent
                {
                    Role = "user",
                    Parts = [new GeminiPart { Text = userPrompt }]
                }
            ]
        };

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            request.SystemInstruction = new GeminiContent
            {
                Parts = [new GeminiPart { Text = systemPrompt }]
            };
        }

        if (jsonMode)
        {
            request.GenerationConfig = new GeminiGenerationConfig { ResponseMimeType = "application/json" };
        }

        return JsonSerializer.Serialize(request, SerializerOptions);
    }

    private static GeminiResponse ParseResponse(string body)
    {
        GeminiApiResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<GeminiApiResponse>(body, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new AiServiceException("The AI service returned an unreadable response.", ex);
        }

        if (parsed is null)
        {
            throw new AiServiceException("The AI service returned an empty response.");
        }

        if (!string.IsNullOrWhiteSpace(parsed.PromptFeedback?.BlockReason))
        {
            throw new AiServiceException("The AI request was blocked by the provider's safety filters.");
        }

        var parts = parsed.Candidates?.FirstOrDefault()?.Content?.Parts;
        if (parts is null || parts.Count == 0)
        {
            throw new AiServiceException("The AI service returned no content.");
        }

        var content = string.Concat(parts.Select(p => p.Text));
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new AiServiceException("The AI service returned no content.");
        }

        var promptTokens = parsed.UsageMetadata?.PromptTokenCount ?? 0;
        var completionTokens = parsed.UsageMetadata?.CandidatesTokenCount ?? 0;

        return new GeminiResponse(
            content,
            promptTokens,
            completionTokens,
            GeminiPricing.Estimate(promptTokens, completionTokens));
    }

    /// <summary>
    /// Distinguishes failures caused by the key itself — which another key may survive — from
    /// request-level failures that would fail identically on every key.
    /// </summary>
    private static bool IsKeyScopedFailure(HttpStatusCode status, string body)
    {
        if (status is HttpStatusCode.TooManyRequests or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return true;
        }

        // A revoked or mistyped key surfaces as 400 INVALID_ARGUMENT with this reason.
        return body.Contains("API_KEY_INVALID", StringComparison.OrdinalIgnoreCase)
            || body.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
            || body.Contains("quota", StringComparison.OrdinalIgnoreCase);
    }

    private async Task RecordLedgerAsync(
        string systemPrompt,
        IntegrationFeature feature,
        Guid userId,
        GeminiResponse result,
        CancellationToken ct)
    {
        try
        {
            await _ledger.RecordAsync(
                new AIHistory
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    IntegrationFeature = feature,
                    // System prompt only: the user prompt and the raw response can carry resume PII.
                    PromptContext = $"{feature}: {systemPrompt}",
                    PromptTokens = result.PromptTokens,
                    CompletionTokens = result.CompletionTokens,
                    TokenCost = result.EstimatedCostUsd,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                ct);
        }
        catch (Exception ex)
        {
            // A ledger failure must not discard a generation the user already paid for.
            _logger.LogError(ex, "Failed to write the AIHistory ledger row for feature {Feature}.", feature);
        }
    }
}
