using Microsoft.AspNetCore.DataProtection;

namespace InternLink.Web.Services.Auth;

// Protects the short-lived reference (userId + issued time + remember flag) that ties a
// password-verified request to the later OTP step, so no auth cookie is issued until OTP passes.
public sealed class PendingLoginTokenService
{
    private readonly IDataProtector _protector;
    private readonly TimeProvider _timeProvider;

    public PendingLoginTokenService(IDataProtectionProvider provider, TimeProvider timeProvider)
    {
        _protector = provider.CreateProtector("InternLink.PendingLogin.v1");
        _timeProvider = timeProvider;
    }

    public string Create(Guid userId, bool remember)
    {
        var issued = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var payload = $"{userId:D}|{issued}|{(remember ? 1 : 0)}";
        return _protector.Protect(payload);
    }

    public bool TryRead(string? token, TimeSpan maxAge, out Guid userId, out bool remember)
    {
        userId = Guid.Empty;
        remember = false;

        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        try
        {
            var payload = _protector.Unprotect(token);
            var parts = payload.Split('|');
            if (parts.Length != 3 || !Guid.TryParse(parts[0], out userId) || !long.TryParse(parts[1], out var issuedUnix))
            {
                return false;
            }

            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedUnix);
            if (_timeProvider.GetUtcNow() - issuedAt > maxAge)
            {
                return false;
            }

            remember = parts[2] == "1";
            return true;
        }
        catch
        {
            return false;
        }
    }
}
