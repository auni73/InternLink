using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using InternLink.Web.Services.AI;
using Xunit;

namespace InternLink.Tests;

public class GeminiKeyPoolTests
{
    [Fact]
    public void TryLease_RoundRobinsAcrossKeys()
    {
        var (pool, _) = BuildPool("a,b,c");

        Assert.True(pool.TryLease(out var first));
        Assert.True(pool.TryLease(out var second));
        Assert.True(pool.TryLease(out var third));
        Assert.True(pool.TryLease(out var fourth));

        Assert.Equal("a", first.ApiKey);
        Assert.Equal("b", second.ApiKey);
        Assert.Equal("c", third.ApiKey);
        Assert.Equal("a", fourth.ApiKey);
    }

    [Fact]
    public void TryLease_SkipsKeysStillCoolingDown()
    {
        var (pool, _) = BuildPool("a,b");

        pool.ReportKeyFailure(0);

        Assert.True(pool.TryLease(out var lease));
        Assert.Equal("b", lease.ApiKey);
    }

    [Fact]
    public void TryLease_ReturnsFalse_WhenEveryKeyIsCooling()
    {
        var (pool, _) = BuildPool("a,b");

        pool.ReportKeyFailure(0);
        pool.ReportKeyFailure(1);

        Assert.False(pool.TryLease(out _));
    }

    [Fact]
    public void TryLease_RecoversAfterCooldownElapses()
    {
        var (pool, time) = BuildPool("a");

        pool.ReportKeyFailure(0);
        Assert.False(pool.TryLease(out _));

        time.Advance(TimeSpan.FromSeconds(61));

        Assert.True(pool.TryLease(out var lease));
        Assert.Equal("a", lease.ApiKey);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("YOUR_GEMINI_API_KEYS_COMMA_SEPARATED", 0)]
    [InlineData("a", 1)]
    [InlineData("a,b , c ", 3)]
    [InlineData("a,a,b", 2)]
    public void KeyCount_ParsesAndSanitizesConfiguredKeys(string configured, int expected)
    {
        var (pool, _) = BuildPool(configured);

        Assert.Equal(expected, pool.KeyCount);
    }

    [Fact]
    public void ReportQuotaExceeded_IgnoresOutOfRangeIndex()
    {
        var (pool, _) = BuildPool("a");

        pool.ReportKeyFailure(7);

        Assert.True(pool.TryLease(out _));
    }

    private static (GeminiKeyPool Pool, FakeTimeProvider Time) BuildPool(string apiKeys)
    {
        var time = new FakeTimeProvider();
        var options = Options.Create(new GeminiOptions { ApiKeys = apiKeys });
        return (new GeminiKeyPool(options, time, NullLogger<GeminiKeyPool>.Instance), time);
    }
}
