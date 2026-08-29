using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Resume;

public interface IResumeAnalysisService
{
    /// <summary>
    /// Scores a resume for ATS readiness. Returns <see cref="AtsScoreResult.Unavailable"/> rather than
    /// throwing when the model cannot produce usable JSON.
    /// </summary>
    Task<AtsScoreResult> GetAtsScoreAsync(
        Guid resumeId,
        Guid studentId,
        Guid? targetJobId,
        CancellationToken ct = default);

    /// <summary>Before/after rewrite pairs tailored to a specific posting.</summary>
    Task<IReadOnlyList<ImprovementSuggestion>> GetImprovementSuggestionsAsync(
        Guid resumeId,
        Guid studentId,
        Guid targetJobId,
        CancellationToken ct = default);
}
