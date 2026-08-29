using System.Text.Json.Serialization;

namespace InternLink.Web.ViewModels;

public sealed class AtsScoreResult
{
    /// <summary>0-100, or -1 when analysis could not be produced. The UI treats -1 as a retry state.</summary>
    [JsonPropertyName("atsScore")]
    public int AtsScore { get; set; }

    [JsonPropertyName("grammarIssues")]
    public List<string> GrammarIssues { get; set; } = [];

    [JsonPropertyName("structureCritique")]
    public string StructureCritique { get; set; } = string.Empty;

    [JsonPropertyName("missingKeywords")]
    public List<string> MissingKeywords { get; set; } = [];

    public const int UnavailableScore = -1;

    public static AtsScoreResult Unavailable() => new()
    {
        AtsScore = UnavailableScore,
        StructureCritique = "Analysis temporarily unavailable. Please try again in a moment."
    };
}

public sealed class ImprovementSuggestion
{
    [JsonPropertyName("originalText")]
    public string OriginalText { get; set; } = string.Empty;

    [JsonPropertyName("suggestedText")]
    public string SuggestedText { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public sealed class ResumeAnalysisResultViewModel
{
    public AtsScoreResult Score { get; set; } = new();
    public IReadOnlyList<ImprovementSuggestion> Suggestions { get; set; } = [];
}

public sealed class TargetJobOption
{
    public Guid JobId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
}

public sealed class ResumeAnalysisPageViewModel
{
    public Guid ResumeId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public bool IsFinalized { get; set; }
    public IReadOnlyList<TargetJobOption> TargetJobs { get; set; } = [];
}
