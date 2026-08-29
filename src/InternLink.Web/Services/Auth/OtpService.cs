using System.Security.Cryptography;
using System.Text;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.Email;

namespace InternLink.Web.Services.Auth;

public sealed class OtpService : IOtpService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(30);

    private readonly IOtpRepository _repository;
    private readonly IEmailSender _emailSender;
    private readonly TimeProvider _timeProvider;

    public OtpService(IOtpRepository repository, IEmailSender emailSender, TimeProvider timeProvider)
    {
        _repository = repository;
        _emailSender = emailSender;
        _timeProvider = timeProvider;
    }

    public async Task SendAsync(Guid userId, string email, CancellationToken ct = default)
    {
        // Only one live code per user: invalidate any existing pending code first.
        var existing = await _repository.FindPendingByUserAsync(userId, ct);
        if (existing is not null)
        {
            await _repository.ConsumeAsync(existing.Id, _timeProvider.GetUtcNow(), ct);
        }

        await GenerateStoreAndEmailAsync(userId, email, ct);
    }

    public async Task<OtpVerifyResult> VerifyAsync(Guid userId, string code, CancellationToken ct = default)
    {
        var pending = await _repository.FindPendingByUserAsync(userId, ct);
        if (pending is null)
        {
            return OtpVerifyResult.InvalidOrExpired;
        }

        var now = _timeProvider.GetUtcNow();
        if (now >= pending.ExpiresAt)
        {
            return OtpVerifyResult.InvalidOrExpired;
        }

        if (!HashMatches(code, pending.CodeHash))
        {
            return OtpVerifyResult.InvalidOrExpired;
        }

        await _repository.ConsumeAsync(pending.Id, now, ct);
        return OtpVerifyResult.Success;
    }

    public async Task<OtpResendResult> ResendAsync(Guid userId, string email, CancellationToken ct = default)
    {
        var pending = await _repository.FindPendingByUserAsync(userId, ct);
        if (pending is null)
        {
            return OtpResendResult.NoPendingLogin;
        }

        var now = _timeProvider.GetUtcNow();
        if (now - pending.LastSentAt < ResendCooldown)
        {
            return OtpResendResult.TooSoon;
        }

        // Resending invalidates the previous code and issues a fresh one.
        await _repository.ConsumeAsync(pending.Id, now, ct);
        await GenerateStoreAndEmailAsync(userId, email, ct);
        return OtpResendResult.Sent;
    }

    private async Task GenerateStoreAndEmailAsync(Guid userId, string email, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow();
        var code = GenerateCode();

        var otp = new OtpCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CodeHash = Hash(code),
            ExpiresAt = now.Add(CodeLifetime),
            ConsumedAt = null,
            CreatedAt = now,
            LastSentAt = now
        };

        await _repository.InsertAsync(otp, ct);

        await _emailSender.SendAsync(
            email,
            "Your InternLink verification code",
            $"Your verification code is <strong>{code}</strong>. It expires in 5 minutes.",
            ct);
    }

    // Cryptographically secure 6-digit code (never System.Random).
    private static string GenerateCode()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }

    private static string Hash(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }

    private static bool HashMatches(string code, string storedHash)
    {
        var candidate = Encoding.UTF8.GetBytes(Hash(code));
        var stored = Encoding.UTF8.GetBytes(storedHash);
        return CryptographicOperations.FixedTimeEquals(candidate, stored);
    }
}
