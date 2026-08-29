using System.Net;
using System.Net.Mail;

namespace InternLink.Web.Services.Email;

public sealed class SmtpEmailSender : IAppEmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var host = _config["Smtp:Host"];
        var fromAddress = _config["Smtp:FromAddress"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromAddress))
        {
            _logger.LogError("SMTP configuration missing (Smtp:Host / Smtp:FromAddress). Email to {To} was not sent.", toEmail);
            return;
        }

        var port = int.TryParse(_config["Smtp:Port"], out var p) ? p : 587;

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_config["Smtp:User"], _config["Smtp:Pass"])
        };

        using var message = new MailMessage(fromAddress, toEmail, subject, htmlBody)
        {
            IsBodyHtml = true
        };

        try
        {
            await client.SendMailAsync(message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}.", toEmail);
        }
    }
}
