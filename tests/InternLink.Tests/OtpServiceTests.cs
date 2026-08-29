using System.Text.RegularExpressions;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.Auth;
using InternLink.Web.Services.Email;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace InternLink.Tests;

public class OtpServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string Email = "user@internlink.test";

    private static (OtpService service, FakeOtpRepository repo, CapturingEmailSender email, FakeTimeProvider time) CreateSut()
    {
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(start);
        var repo = new FakeOtpRepository();
        var email = new CapturingEmailSender();
        var service = new OtpService(repo, email, time);
        return (service, repo, email, time);
    }

    [Fact]
    public async Task ExpiredCode_IsRejected()
    {
        var (service, _, email, time) = CreateSut();
        await service.SendAsync(UserId, Email);

        // Past the 5-minute lifetime.
        time.Advance(TimeSpan.FromMinutes(6));

        var result = await service.VerifyAsync(UserId, email.LastCode!);

        Assert.Equal(OtpVerifyResult.InvalidOrExpired, result);
    }

    [Fact]
    public async Task ValidCode_SucceedsExactlyOnce()
    {
        var (service, _, email, time) = CreateSut();
        await service.SendAsync(UserId, Email);

        time.Advance(TimeSpan.FromMinutes(1));

        var first = await service.VerifyAsync(UserId, email.LastCode!);
        var second = await service.VerifyAsync(UserId, email.LastCode!);

        Assert.Equal(OtpVerifyResult.Success, first);
        Assert.Equal(OtpVerifyResult.InvalidOrExpired, second);
    }

    [Fact]
    public async Task Resend_Within30Seconds_IsRejected()
    {
        var (service, _, _, time) = CreateSut();
        await service.SendAsync(UserId, Email);

        time.Advance(TimeSpan.FromSeconds(10));

        var result = await service.ResendAsync(UserId, Email);

        Assert.Equal(OtpResendResult.TooSoon, result);
    }

    [Fact]
    public async Task Resend_After30Seconds_IssuesNewCode()
    {
        var (service, _, email, time) = CreateSut();
        await service.SendAsync(UserId, Email);
        var firstCode = email.LastCode;

        time.Advance(TimeSpan.FromSeconds(31));
        var result = await service.ResendAsync(UserId, Email);

        Assert.Equal(OtpResendResult.Sent, result);
        // The old code is invalidated; the new one verifies.
        Assert.Equal(OtpVerifyResult.InvalidOrExpired, await service.VerifyAsync(UserId, firstCode!));
        Assert.Equal(OtpVerifyResult.Success, await service.VerifyAsync(UserId, email.LastCode!));
    }

    // ---- Test doubles ----

    private sealed class CapturingEmailSender : IEmailSender
    {
        public string? LastCode { get; private set; }

        public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        {
            var match = Regex.Match(htmlBody, @"\d{6}");
            if (match.Success)
            {
                LastCode = match.Value;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOtpRepository : IOtpRepository
    {
        private readonly List<OtpCode> _codes = new();

        public Task InsertAsync(OtpCode code, CancellationToken ct = default)
        {
            _codes.Add(code);
            return Task.CompletedTask;
        }

        public Task<OtpCode?> FindPendingByUserAsync(Guid userId, CancellationToken ct = default)
        {
            var pending = _codes
                .Where(c => c.UserId == userId && c.ConsumedAt is null)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefault();
            return Task.FromResult(pending);
        }

        public Task ConsumeAsync(Guid id, DateTimeOffset consumedAt, CancellationToken ct = default)
        {
            var code = _codes.FirstOrDefault(c => c.Id == id && c.ConsumedAt is null);
            if (code is not null)
            {
                code.ConsumedAt = consumedAt;
            }
            return Task.CompletedTask;
        }
    }
}
