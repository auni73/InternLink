namespace InternLink.Web.Models;

public class OtpCode
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSentAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual AppUser User { get; set; } = null!;
}
