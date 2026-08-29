using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace InternLink.Web.Services.Email;

public sealed class MailKitEmailSender : IEmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(IConfiguration config, ILogger<MailKitEmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var host = _config["Smtp:Host"];
        var fromAddress = _config["Smtp:FromAddress"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromAddress))
        {
            _logger.LogError("SMTP configuration missing (Smtp:Host / Smtp:FromAddress). Email to {To} was not sent.", to);
            return;
        }

        var port = int.TryParse(_config["Smtp:Port"], out var parsedPort) ? parsedPort : 587;
        var user = _config["Smtp:User"];
        var pass = _config["Smtp:Pass"];

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(fromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls, ct);
            if (!string.IsNullOrWhiteSpace(user))
            {
                await client.AuthenticateAsync(user, pass ?? string.Empty, ct);
            }
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}.", to);
        }
    }
}
