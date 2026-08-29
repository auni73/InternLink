namespace InternLink.Web.Services.Auth;

public enum OtpVerifyResult
{
    Success,
    InvalidOrExpired
}

public enum OtpResendResult
{
    Sent,
    TooSoon,
    NoPendingLogin
}

public interface IOtpService
{
    Task SendAsync(Guid userId, string email, CancellationToken ct = default);
    Task<OtpVerifyResult> VerifyAsync(Guid userId, string code, CancellationToken ct = default);
    Task<OtpResendResult> ResendAsync(Guid userId, string email, CancellationToken ct = default);
}
