using InternLink.Web.Models.Enums;

namespace InternLink.Web.Models;

public class Job
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CoreDescription { get; set; } = string.Empty;
    public string SelectionCriteria { get; set; } = string.Empty;
    public LocationType LocationType { get; set; }
    public DateTimeOffset DeadLine { get; set; }
    public bool IsApproved { get; set; }
    public bool IsClosed { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual Company Company { get; set; } = null!;
    public virtual ICollection<Application> Applications { get; set; } = new List<Application>();
    public virtual ICollection<JobSkill> JobSkills { get; set; } = new List<JobSkill>();
}
