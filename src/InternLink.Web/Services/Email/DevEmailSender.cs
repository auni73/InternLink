namespace InternLink.Web.Services.Email;

// Development sender: writes the email (OTP codes, confirmation links) to the console instead of sending.
public sealed class DevEmailSender : IEmailSender
{
    private readonly ILogger<DevEmailSender> _logger;

    public DevEmailSender(ILogger<DevEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[DEV OTP]\n  To: {To}\n  Subject: {Subject}\n  Body:\n{Body}",
            to, subject, htmlBody);
        return Task.CompletedTask;
    }
}
