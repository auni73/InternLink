using InternLink.Web.Models.Enums;

namespace InternLink.Web.ViewModels;

/// <summary>A candidate job pulled from the relational side to score against a vector hit.</summary>
public sealed class RecommendationCandidate
{
    public Guid JobId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public LocationType LocationType { get; set; }
    public DateTimeOffset DeadLine { get; set; }
    public int RequiredSkillCount { get; set; }
    public int MatchedSkillCount { get; set; }
    public string? TopMatchedSkillName { get; set; }
    public bool HasApplied { get; set; }
}

public sealed class RecommendedJobViewModel
{
    public Guid JobId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string LocationType { get; set; } = string.Empty;
    public DateTimeOffset DeadLine { get; set; }
    public int MatchPercentage { get; set; }
    public int MatchedSkillCount { get; set; }
    public int RequiredSkillCount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class RecommendationResultViewModel
{
    /// <summary>True when semantic search was unavailable and results came from relational skill overlap.</summary>
    public bool Degraded { get; set; }

    public IReadOnlyList<RecommendedJobViewModel> Jobs { get; set; } = [];
}
