namespace InternLink.Web.Models;

public class StudentSkill
{
    public Guid StudentId { get; set; }
    public Guid SkillId { get; set; }
    public int ProficiencyLevel { get; set; }

    // Navigation properties
    public virtual Student Student { get; set; } = null!;
    public virtual Skill Skill { get; set; } = null!;
}
