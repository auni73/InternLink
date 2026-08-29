using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.AI;
using Xunit;

namespace InternLink.Tests;

public class GeminiGatewayTests
{
    private const string SuccessBody = """
    {
      "candidates": [
        { "content": { "parts": [ { "text": "Generated answer." } ] }, "finishReason": "STOP" }
      ],
      "usageMetadata": { "promptTokenCount": 120, "candidatesTokenCount": 45, "totalTokenCount": 165 }
    }
    """;

    private const string QuotaBody = """
    { "error": { "code": 429, "status": "RESOURCE_EXHAUSTED", "message": "Quota exceeded for this key." } }
    """;

    [Fact]
    public async Task GenerateAsync_RotatesToSecondKey_WhenFirstKeyReturns429()
    {
        var handler = new FakeHttpMessageHandler((_, callIndex) => callIndex == 0
            ? Respond(HttpStatusCode.TooManyRequests, QuotaBody)
            : Respond(HttpStatusCode.OK, SuccessBody));

        var (client, ledger, _) = BuildClient(handler, "key-one,key-two");

        var result = await client.GenerateAsync("system", "user", IntegrationFeature.CoverLetter, Guid.NewGuid(), false);

        Assert.Equal("Generated answer.", result.Content);
        Assert.Equal(2, handler.CallCount);
        Assert.Equal("key-one", handler.ReceivedApiKeys[0]);
        Assert.Equal("key-two", handler.ReceivedApiKeys[1]);
        Assert.Single(ledger.Entries);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsPromptly_WhenEveryKeyIsCoolingDown()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Respond(HttpStatusCode.OK, SuccessBody));
        var (client, _, pool) = BuildClient(handler, "key-one,key-two");

        pool.ReportQuotaExceeded(0);
        pool.ReportQuotaExceeded(1);

        var ex = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.GenerateAsync("system", "user", IntegrationFeature.SkillGap, Guid.NewGuid(), false));

        Assert.Contains("temporarily busy", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_WrapsTransportFailure_InAiServiceException()
    {
        var handler = new FakeHttpMessageHandler((_, _) => throw new HttpRequestException("socket closed"));
        var (client, _, _) = BuildClient(handler, "key-one");

        var ex = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.GenerateAsync("system", "user", IntegrationFeature.AtsScoring, Guid.NewGuid(), false));

        Assert.IsType<HttpRequestException>(ex.InnerException);
        // Two retries on top of the initial attempt.
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_WritesLedgerRow_WithRealTokenCounts()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Respond(HttpStatusCode.OK, SuccessBody));
        var (client, ledger, _) = BuildClient(handler, "key-one");
        var userId = Guid.NewGuid();

        var result = await client.GenerateAsync("Score this resume.", "PII resume text", IntegrationFeature.AtsScoring, userId, true);

        var entry = Assert.Single(ledger.Entries);
        Assert.Equal(userId, entry.UserId);
        Assert.Equal(IntegrationFeature.AtsScoring, entry.IntegrationFeature);
        Assert.Equal(120, entry.PromptTokens);
        Assert.Equal(45, entry.CompletionTokens);
        Assert.True(entry.TokenCost > 0m);
        Assert.Equal(result.EstimatedCostUsd, entry.TokenCost);
        // The user prompt can carry resume PII and must never reach the ledger.
        Assert.DoesNotContain("PII resume text", entry.PromptContext);
    }

    [Fact]
    public async Task GenerateAsync_DoesNotRetry_OnBadRequest()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            Respond(HttpStatusCode.BadRequest, """{ "error": { "code": 400, "message": "Invalid argument" } }"""));

        var (client, ledger, _) = BuildClient(handler, "key-one,key-two");

        await Assert.ThrowsAsync<AiServiceException>(() =>
            client.GenerateAsync("system", "user", IntegrationFeature.QuestionBank, Guid.NewGuid(), false));

        Assert.Equal(1, handler.CallCount);
        Assert.Empty(ledger.Entries);
    }

    [Fact]
    public async Task GenerateAsync_RetriesOnServerError_ThenSucceeds()
    {
        var handler = new FakeHttpMessageHandler((_, callIndex) => callIndex == 0
            ? Respond(HttpStatusCode.ServiceUnavailable, "{}")
            : Respond(HttpStatusCode.OK, SuccessBody));

        var (client, ledger, _) = BuildClient(handler, "key-one");

        var result = await client.GenerateAsync("system", "user", IntegrationFeature.MockInterview, Guid.NewGuid(), false);

        Assert.Equal("Generated answer.", result.Content);
        Assert.Equal(2, handler.CallCount);
        Assert.Single(ledger.Entries);
    }

    [Fact]
    public async Task GenerateAsync_Throws_WhenNoKeysConfigured()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Respond(HttpStatusCode.OK, SuccessBody));
        var (client, _, _) = BuildClient(handler, "YOUR_GEMINI_API_KEYS_COMMA_SEPARATED");

        var ex = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.GenerateAsync("system", "user", IntegrationFeature.JobRecommendations, Guid.NewGuid(), false));

        Assert.Contains("not configured", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_SendsJsonMimeType_OnlyWhenJsonModeRequested()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Respond(HttpStatusCode.OK, SuccessBody));
        var (client, _, _) = BuildClient(handler, "key-one");

        await client.GenerateAsync("system", "user", IntegrationFeature.ResumeSuggestions, Guid.NewGuid(), true);
        Assert.Contains("application/json", handler.ReceivedBodies[0]);

        await client.GenerateAsync("system", "user", IntegrationFeature.ResumeSuggestions, Guid.NewGuid(), false);
        Assert.DoesNotContain("responseMimeType", handler.ReceivedBodies[1]);
    }

    private static (GeminiClient Client, FakeAIHistoryRepository Ledger, GeminiKeyPool Pool) BuildClient(
        FakeHttpMessageHandler handler,
        string apiKeys)
    {
        var options = Options.Create(new GeminiOptions
        {
            ApiKeys = apiKeys,
            Model = "gemini-2.5-flash",
            RetryBaseDelayMilliseconds = 0
        });

        var pool = new GeminiKeyPool(options, new FakeTimeProvider(), NullLogger<GeminiKeyPool>.Instance);
        var ledger = new FakeAIHistoryRepository();

        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
        };

        var client = new GeminiClient(http, pool, ledger, options, NullLogger<GeminiClient>.Instance);
        return (client, ledger, pool);
    }

    private static HttpResponseMessage Respond(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _responder;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public int CallCount { get; private set; }

    public List<string?> ReceivedApiKeys { get; } = [];

    public List<string?> ReceivedBodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var callIndex = CallCount++;
        ReceivedApiKeys.Add(request.Headers.TryGetValues("x-goog-api-key", out var values)
            ? values.FirstOrDefault()
            : null);

        ReceivedBodies.Add(request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken));

        return _responder(request, callIndex);
    }
}

internal sealed class FakeAIHistoryRepository : IAIHistoryRepository
{
    public List<AIHistory> Entries { get; } = [];

    public Task RecordAsync(AIHistory entry, CancellationToken ct = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
}
