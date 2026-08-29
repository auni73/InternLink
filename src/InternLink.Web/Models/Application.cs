using InternLink.Web.Models.Enums;

namespace InternLink.Web.Models;

public class Application
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid StudentId { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public ApplicationStatus ApplicationStatus { get; set; } = ApplicationStatus.Applied;
    public Guid? AttachedResumeId { get; set; }
    public string? CoverLetterText { get; set; }

    // Navigation properties
    public virtual Job Job { get; set; } = null!;
    public virtual Student Student { get; set; } = null!;
    public virtual Resume? AttachedResume { get; set; }
    public virtual ICollection<Interview> Interviews { get; set; } = new List<Interview>();
}
