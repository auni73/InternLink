using System.Text.Json.Serialization;
using InternLink.Web.Models.Enums;

namespace InternLink.Web.ViewModels;

public sealed class JobRequiredSkillRow
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public byte Domain { get; set; }
    public byte Weight { get; set; }
}

public sealed class StudentHeldSkillRow
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public byte Domain { get; set; }
    public int ProficiencyLevel { get; set; }
    public bool IsVerified { get; set; }
}

public sealed class ApplicationSkillGapScope
{
    public Guid StudentId { get; set; }
    public Guid JobId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
}

/// <summary>Who is reading the panel. Only the wording changes; the numbers are identical.</summary>
public enum SkillGapPerspective
{
    Student = 0,
    Company = 1
}

public sealed class SkillGapSkill
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public SkillDomain Domain { get; set; }
    public int? ProficiencyLevel { get; set; }
    public bool IsVerified { get; set; }
    public int? Weight { get; set; }

    /// <summary>Weight 4 and 5 are the requirements a recruiter will not compromise on.</summary>
    public bool IsMustHave => Weight >= SkillGapResult.MustHaveWeight;

    public IReadOnlyList<string> LearningResources { get; set; } = [];
}

public sealed class SkillGapResult
{
    public const int MustHaveWeight = 4;

    public Guid StudentId { get; set; }
    public Guid JobId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public SkillGapPerspective Perspective { get; set; }

    public IReadOnlyList<SkillGapSkill> Have { get; set; } = [];
    public IReadOnlyList<SkillGapSkill> Needed { get; set; } = [];
    public IReadOnlyList<SkillGapSkill> Matched { get; set; } = [];
    public IReadOnlyList<SkillGapSkill> Gap { get; set; } = [];

    /// <summary>False when the model failed. The sets above are unaffected: they never involve AI.</summary>
    public bool SuggestionsAvailable { get; set; }

    public int MustHaveGapCount => Gap.Count(s => s.IsMustHave);
    public double MatchPercentage => Needed.Count == 0 ? 100 : Math.Round(Matched.Count * 100.0 / Needed.Count);
}

public sealed class SkillGapSuggestionBatch
{
    [JsonPropertyName("suggestions")]
    public List<SkillGapSuggestion> Suggestions { get; set; } = [];
}

public sealed class SkillGapSuggestion
{
    [JsonPropertyName("skillName")]
    public string SkillName { get; set; } = string.Empty;

    [JsonPropertyName("learningResources")]
    public List<string> LearningResources { get; set; } = [];
}
