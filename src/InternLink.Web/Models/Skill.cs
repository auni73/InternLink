using InternLink.Web.Models.Enums;

namespace InternLink.Web.Models;

public class Skill
{
    public Guid Id { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public SkillDomain DomainClassification { get; set; }

    // Navigation properties
    public virtual ICollection<StudentSkill> StudentSkills { get; set; } = new List<StudentSkill>();
    public virtual ICollection<JobSkill> JobSkills { get; set; } = new List<JobSkill>();
    public virtual ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();
}
