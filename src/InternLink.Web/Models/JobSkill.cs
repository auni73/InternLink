namespace InternLink.Web.Models;

public class JobSkill
{
    public Guid JobId { get; set; }
    public Guid SkillId { get; set; }
    public int RequiredImportanceWeight { get; set; }

    // Navigation properties
    public virtual Job Job { get; set; } = null!;
    public virtual Skill Skill { get; set; } = null!;
}
