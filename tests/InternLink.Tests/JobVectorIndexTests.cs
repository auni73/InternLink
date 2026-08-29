using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using InternLink.Web.Services.AI;
using InternLink.Web.Services.Vectors;
using Xunit;

namespace InternLink.Tests;

public class JobVectorIndexTests
{
    // ---------------------------------------------------------------- document shaping

    [Fact]
    public void ToDocumentText_UsesTitleDescriptionCriteriaAndWeightOrderedSkills()
    {
        var source = new JobVectorSource
        {
            Title = "Junior .NET Developer Intern",
            CoreDescription = "Build ASP.NET Core MVC applications.",
            SelectionCriteria = "Strong C# fundamentals.",
            SkillNames = ["C#", "ASP.NET Core", "SQL Server"]
        };

        Assert.Equal(
            "Junior .NET Developer Intern\nBuild ASP.NET Core MVC applications.\nStrong C# fundamentals.\nSkills: C#, ASP.NET Core, SQL Server",
            source.ToDocumentText());
    }

    [Fact]
    public void ToPayload_ProjectsDeadlineToUnixSeconds()
    {
        var deadline = new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var companyId = Guid.NewGuid();
        var skillId = Guid.NewGuid();

        var payload = new JobVectorSource
        {
            CompanyId = companyId,
            LocationType = 2,
            DeadLine = deadline,
            SkillIds = [skillId]
        }.ToPayload();

        Assert.Equal(companyId, payload.CompanyId);
        Assert.Equal(2, payload.LocationType);
        Assert.Equal(deadline.ToUnixTimeSeconds(), payload.DeadlineUnix);
        Assert.Equal([skillId], payload.SkillIds);
    }

    // ---------------------------------------------------------------- queue

    [Fact]
    public async Task Queue_RoundTripsCommandsInOrder()
    {
        var queue = new JobIndexQueue(NullLogger<JobIndexQueue>.Instance);
        var first = new JobIndexCommand(Guid.NewGuid(), JobIndexOperation.Upsert);
        var second = new JobIndexCommand(Guid.NewGuid(), JobIndexOperation.Delete);

        Assert.True(queue.TryEnqueue(first));
        Assert.True(queue.TryEnqueue(second));

        using var cts = new CancellationTokenSource();
        var received = new List<JobIndexCommand>();
        await foreach (var command in queue.ReadAllAsync(cts.Token))
        {
            received.Add(command);
            if (received.Count == 2)
            {
                cts.Cancel();
                break;
            }
        }

        Assert.Equal([first, second], received);
    }

    [Fact]
    public void Queue_DropsWritesWhenFull_RatherThanBlockingTheRequest()
    {
        var queue = new JobIndexQueue(NullLogger<JobIndexQueue>.Instance);

        for (var i = 0; i < 1000; i++)
        {
            Assert.True(queue.TryEnqueue(new JobIndexCommand(Guid.NewGuid(), JobIndexOperation.Upsert)));
        }

        // Capacity reached: the write is dropped and reported, never awaited.
        Assert.False(queue.TryEnqueue(new JobIndexCommand(Guid.NewGuid(), JobIndexOperation.Upsert)));
    }

    // ---------------------------------------------------------------- store guard rails

    [Fact]
    public async Task Store_IsNotConfigured_WhenSettingsArePlaceholders()
    {
        var store = new QdrantJobVectorStore(
            Options.Create(new QdrantOptions
            {
                Endpoint = "https://your-qdrant-cluster-url.qdrant.tech",
                ApiKey = "YOUR_QDRANT_API_KEY"
            }),
            NullLogger<QdrantJobVectorStore>.Instance);

        Assert.False(store.IsConfigured);
        await Assert.ThrowsAsync<SemanticSearchUnavailableException>(() => store.EnsureCollectionAsync());
        await Assert.ThrowsAsync<SemanticSearchUnavailableException>(() => store.SearchJobsAsync(new float[768], 5));
        await Assert.ThrowsAsync<SemanticSearchUnavailableException>(() => store.DeleteJobAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Store_RejectsVectorsOfTheWrongDimension()
    {
        var store = new QdrantJobVectorStore(
            Options.Create(new QdrantOptions { Endpoint = "https://example.cloud.qdrant.io", ApiKey = "k" }),
            NullLogger<QdrantJobVectorStore>.Instance);

        var ex = await Assert.ThrowsAsync<SemanticSearchUnavailableException>(() =>
            store.UpsertJobAsync(Guid.NewGuid(), new float[512], NewPayload()));

        Assert.Contains("768", ex.Message);
    }

    // ---------------------------------------------------------------- embedding client

    [Theory]
    [InlineData(EmbeddingPurpose.Document, "RETRIEVAL_DOCUMENT")]
    [InlineData(EmbeddingPurpose.Query, "RETRIEVAL_QUERY")]
    public async Task Embedder_MapsPurposeToAsymmetricTaskType(EmbeddingPurpose purpose, string expectedTaskType)
    {
        var handler = new FakeHttpMessageHandler((_, _) => Respond(HttpStatusCode.OK, EmbeddingBody(768)));
        var client = BuildEmbedder(handler, "key-one");

        await client.EmbedAsync("some text", purpose);

        Assert.Contains(expectedTaskType, handler.ReceivedBodies[0]);
        Assert.Contains("\"outputDimensionality\":768", handler.ReceivedBodies[0]);
    }

    [Fact]
    public async Task Embedder_ReturnsVector_OnSuccess()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Respond(HttpStatusCode.OK, EmbeddingBody(768)));
        var client = BuildEmbedder(handler, "key-one");

        var vector = await client.EmbedAsync("some text", EmbeddingPurpose.Document);

        Assert.Equal(768, vector.Length);
    }

    [Fact]
    public async Task Embedder_Throws_WhenDimensionsDoNotMatchTheCollection()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Respond(HttpStatusCode.OK, EmbeddingBody(1536)));
        var client = BuildEmbedder(handler, "key-one");

        await Assert.ThrowsAsync<SemanticSearchUnavailableException>(() =>
            client.EmbedAsync("some text", EmbeddingPurpose.Document));
    }

    [Fact]
    public async Task Embedder_RotatesPastAnInvalidKey()
    {
        const string invalidKey = """{ "error": { "code": 400, "details": [ { "reason": "API_KEY_INVALID" } ] } }""";

        var handler = new FakeHttpMessageHandler((_, callIndex) => callIndex == 0
            ? Respond(HttpStatusCode.BadRequest, invalidKey)
            : Respond(HttpStatusCode.OK, EmbeddingBody(768)));

        var client = BuildEmbedder(handler, "garbage,key-two");

        var vector = await client.EmbedAsync("some text", EmbeddingPurpose.Document);

        Assert.Equal(768, vector.Length);
        Assert.Equal("garbage", handler.ReceivedApiKeys[0]);
        Assert.Equal("key-two", handler.ReceivedApiKeys[1]);
    }

    [Fact]
    public async Task Embedder_Throws_WhenNoKeysConfigured()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Respond(HttpStatusCode.OK, EmbeddingBody(768)));
        var client = BuildEmbedder(handler, "YOUR_GEMINI_API_KEYS_COMMA_SEPARATED");

        await Assert.ThrowsAsync<SemanticSearchUnavailableException>(() =>
            client.EmbedAsync("some text", EmbeddingPurpose.Document));

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Embedder_WrapsTransportFailure_InTypedException()
    {
        var handler = new FakeHttpMessageHandler((_, _) => throw new HttpRequestException("socket closed"));
        var client = BuildEmbedder(handler, "key-one");

        var ex = await Assert.ThrowsAsync<SemanticSearchUnavailableException>(() =>
            client.EmbedAsync("some text", EmbeddingPurpose.Query));

        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    // ---------------------------------------------------------------- helpers

    private static JobVectorPayload NewPayload() =>
        new(Guid.NewGuid(), 0, DateTimeOffset.UtcNow.AddDays(10).ToUnixTimeSeconds(), []);

    private static GeminiEmbeddingClient BuildEmbedder(FakeHttpMessageHandler handler, string apiKeys)
    {
        var options = Options.Create(new GeminiOptions
        {
            ApiKeys = apiKeys,
            EmbeddingModel = "gemini-embedding-2",
            RetryBaseDelayMilliseconds = 0
        });

        var pool = new GeminiKeyPool(options, new FakeTimeProvider(), NullLogger<GeminiKeyPool>.Instance);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/") };

        return new GeminiEmbeddingClient(http, pool, options, NullLogger<GeminiEmbeddingClient>.Instance);
    }

    private static string EmbeddingBody(int dimensions) =>
        $$"""{ "embedding": { "values": [{{string.Join(",", Enumerable.Repeat("0.01", dimensions))}}] } }""";

    private static HttpResponseMessage Respond(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
