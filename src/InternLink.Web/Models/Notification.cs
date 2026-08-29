namespace InternLink.Web.Models;

public class Notification
{
    public Guid Id { get; set; }
    public Guid TargetUserId { get; set; }
    public string TextPayload { get; set; } = string.Empty;
    public string EventRoutingUrl { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTimeOffset TimeTriggered { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual AppUser TargetUser { get; set; } = null!;
}
