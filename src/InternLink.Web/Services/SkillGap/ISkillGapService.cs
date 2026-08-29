using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.SkillGap;

public interface ISkillGapService
{
    /// <summary>
    /// Computes have/need/matched/gap deterministically, then asks the model for learning pointers
    /// on the gap only. Never returns null: an AI failure only clears <see cref="SkillGapResult.SuggestionsAvailable"/>.
    /// </summary>
    Task<SkillGapResult> AnalyzeAsync(
        Guid studentId,
        Guid jobId,
        SkillGapPerspective perspective,
        CancellationToken ct = default);
}
