using System.Collections.Concurrent;

namespace InternLink.Web.Services.Auth;

// Development-only convenience: DevEmailSender drops the latest OTP per email here
// so the VerifyOtp page can pre-fill it. Never written to in Production (MailKit path).
public sealed class DevOtpStore
{
    private readonly ConcurrentDictionary<string, string> _codes = new(StringComparer.OrdinalIgnoreCase);

    public void Set(string email, string code) => _codes[email] = code;

    public string? Get(string email) => _codes.TryGetValue(email, out var code) ? code : null;
}
