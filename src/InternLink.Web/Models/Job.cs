using InternLink.Web.Models.Enums;

namespace InternLink.Web.Models;

public class Job
{
    public Guid Id { get; set; }
    public Guid? CompanyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CoreDescription { get; set; } = string.Empty;
    public string SelectionCriteria { get; set; } = string.Empty;
    public LocationType LocationType { get; set; }
    public DateTimeOffset DeadLine { get; set; }
    public bool IsApproved { get; set; }
    public bool IsClosed { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // External Posting Metadata
    public JobSource Source { get; set; } = JobSource.Internal;
    public string? ExternalSourceName { get; set; }
    public string? ExternalJobId { get; set; }
    public string? ExternalApplyUrl { get; set; }
    public string? CompanyNameSnapshot { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }

    // Navigation properties
    public virtual Company? Company { get; set; }
    public virtual ICollection<Application> Applications { get; set; } = new List<Application>();
    public virtual ICollection<JobSkill> JobSkills { get; set; } = new List<JobSkill>();
}
