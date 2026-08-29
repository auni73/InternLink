using InternLink.Web.Models.Enums;

namespace InternLink.Web.Models;

public class MockInterviewSession
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string Role { get; set; } = string.Empty;
    public Guid? JobId { get; set; }
    public string TranscriptJson { get; set; } = "[]";
    public MockInterviewStatus Status { get; set; } = MockInterviewStatus.InProgress;
    public string? ReportJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    // Navigation properties
    public virtual Student Student { get; set; } = null!;
    public virtual Job? Job { get; set; }
}
