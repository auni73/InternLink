using System.Text.RegularExpressions;
using InternLink.Web.Services.Auth;

namespace InternLink.Web.Services.Email;

// Development sender: writes the email (OTP codes, confirmation links) to the console instead of sending.
public sealed class DevEmailSender : IEmailSender
{
    private readonly ILogger<DevEmailSender> _logger;
    private readonly DevOtpStore _otpStore;

    public DevEmailSender(ILogger<DevEmailSender> logger, DevOtpStore otpStore)
    {
        _logger = logger;
        _otpStore = otpStore;
    }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[DEV OTP]\n  To: {To}\n  Subject: {Subject}\n  Body:\n{Body}",
            to, subject, htmlBody);

        // Capture the OTP so the verify page can auto-fill it in Development.
        if (subject.Contains("verification code", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(htmlBody, @"\d{6}");
            if (match.Success)
            {
                _otpStore.Set(to, match.Value);
            }
        }

        return Task.CompletedTask;
    }
}
