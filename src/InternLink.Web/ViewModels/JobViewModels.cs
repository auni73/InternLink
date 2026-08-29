using System.ComponentModel.DataAnnotations;
using InternLink.Web.Models.Enums;

namespace InternLink.Web.ViewModels;

public class JobSearchFilter
{
    public string? Keyword { get; set; }
    public LocationType? LocationType { get; set; }
    public bool RelevantToMe { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 9;
}

public class JobSkillBadgeViewModel
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public int ImportanceWeight { get; set; }
}

public class JobListItemViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? IndustrySector { get; set; }
    public LocationType LocationType { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public bool HasApplied { get; set; }
    public List<JobSkillBadgeViewModel> RequiredSkills { get; set; } = new();

    public bool IsUrgent => (Deadline - DateTimeOffset.UtcNow).TotalHours is > 0 and < 48;
    public string DeadlineFormatted
    {
        get
        {
            var diff = Deadline - DateTimeOffset.UtcNow;
            if (diff.TotalDays >= 1)
            {
                return $"Closes in {(int)diff.TotalDays} days";
            }
            if (diff.TotalHours >= 1)
            {
                return $"Closes in {(int)diff.TotalHours} hours";
            }
            if (diff.TotalMinutes > 0)
            {
                return $"Closes in {(int)diff.TotalMinutes} mins";
            }
            return "Closed";
        }
    }
}

public class JobListViewModel
{
    public IReadOnlyList<JobListItemViewModel> Jobs { get; set; } = new List<JobListItemViewModel>();
    public JobSearchFilter Filter { get; set; } = new();
    public int TotalCount { get; set; }
    public int CurrentPage => Filter.Page;
    public int PageSize => Filter.PageSize;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool IsFtsFallback { get; set; }
}

public class JobDetailViewModel
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? CorporateWebsite { get; set; }
    public string IndustrySector { get; set; } = string.Empty;
    public LocationType LocationType { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public string CoreDescription { get; set; } = string.Empty;
    public string SelectionCriteria { get; set; } = string.Empty;
    public bool HasApplied { get; set; }
    public List<JobSkillBadgeViewModel> RequiredSkills { get; set; } = new();
    public IReadOnlyList<ResumeItemViewModel> FinalizedResumes { get; set; } = new List<ResumeItemViewModel>();

    public bool IsUrgent => (Deadline - DateTimeOffset.UtcNow).TotalHours is > 0 and < 48;
    public string DeadlineFormatted
    {
        get
        {
            var diff = Deadline - DateTimeOffset.UtcNow;
            if (diff.TotalDays >= 1)
            {
                return $"Closes in {(int)diff.TotalDays} days ({Deadline:MMM dd, yyyy})";
            }
            if (diff.TotalHours >= 1)
            {
                return $"Closes in {(int)diff.TotalHours} hours";
            }
            return $"Deadline: {Deadline:MMM dd, yyyy}";
        }
    }
}

public class ApplyJobRequestDto
{
    [Required]
    public Guid ResumeId { get; set; }
    public string? CoverLetterText { get; set; }
}
