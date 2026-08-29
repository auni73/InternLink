namespace InternLink.Web.Services.Email;

// Development sender: logs the email (including confirmation links) to the console instead of sending.
public sealed class DevConsoleEmailSender : IAppEmailSender
{
    private readonly ILogger<DevConsoleEmailSender> _logger;

    public DevConsoleEmailSender(ILogger<DevConsoleEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[DEV EMAIL]\n  To: {To}\n  Subject: {Subject}\n  Body:\n{Body}",
            toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }
}
