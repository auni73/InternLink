namespace InternLink.Web.Models;

public class CounselorFeedback
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid CounselorUserId { get; set; }
    public string NarrativeMarkdown { get; set; } = string.Empty;
    public DateTimeOffset MeetingDate { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual Student Student { get; set; } = null!;
    public virtual AppUser CounselorUser { get; set; } = null!;
}
