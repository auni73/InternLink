namespace InternLink.Web.Models;

public class Assessment
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid SkillId { get; set; }
    public int AchievedScore { get; set; }
    public DateTimeOffset EarnedDate { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual Student Student { get; set; } = null!;
    public virtual Skill Skill { get; set; } = null!;
}
