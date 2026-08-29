namespace InternLink.Web.Services.Email;

public interface IAppEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}
