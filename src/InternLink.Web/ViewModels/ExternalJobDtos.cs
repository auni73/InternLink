using System.ComponentModel.DataAnnotations;
using InternLink.Web.Models.Enums;

namespace InternLink.Web.ViewModels;

public class ExternalJobSourceSyncStatsDto
{
    public string SourceName { get; set; } = string.Empty;
    public int Ingested { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ExternalJobSyncResultDto
{
    public int Ingested { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public DateTimeOffset SyncedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<string> SourcesQueried { get; set; } = new();
    public List<ExternalJobSourceSyncStatsDto> SourceStats { get; set; } = new();
}

public class CreateExternalJobRequestDto
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string CompanyNameSnapshot { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ExternalSourceName { get; set; } = "Manual";

    [Required, MaxLength(1000)]
    public string ExternalApplyUrl { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ExternalJobId { get; set; }

    public LocationType LocationType { get; set; } = LocationType.OnSite;

    public DateTimeOffset DeadLine { get; set; } = DateTimeOffset.UtcNow.AddDays(30);

    [Required]
    public string CoreDescription { get; set; } = string.Empty;

    public string SelectionCriteria { get; set; } = string.Empty;

    public List<string>? SkillNames { get; set; } = new();
}

public class ParseExternalJobRequestDto
{
    [Required]
    public string RawContent { get; set; } = string.Empty;

    public string? SourceName { get; set; }
    public string? SourceUrl { get; set; }
}

public class ParseExternalJobResponseDto
{
    public string Title { get; set; } = string.Empty;
    public string CompanyNameSnapshot { get; set; } = string.Empty;
    public LocationType LocationType { get; set; } = LocationType.OnSite;
    public DateTimeOffset? DeadLine { get; set; }
    public string CoreDescription { get; set; } = string.Empty;
    public string SelectionCriteria { get; set; } = string.Empty;
    public List<string> SuggestedSkillNames { get; set; } = new();
}
