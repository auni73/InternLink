using InternLink.Web.Models.Enums;

namespace InternLink.Web.Models;

public class Interview
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public DateTimeOffset ScheduledDateTime { get; set; }
    public string ContextMeetingLink { get; set; } = string.Empty;
    public InterviewStatus StatusIndicator { get; set; } = InterviewStatus.Scheduled;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual Application Application { get; set; } = null!;
}
