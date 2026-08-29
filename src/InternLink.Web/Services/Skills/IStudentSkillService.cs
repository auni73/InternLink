using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Skills;

public interface IStudentSkillService
{
    Task<IReadOnlyList<SkillAssessmentListItemViewModel>> GetStudentSkillAssessmentsAsync(Guid studentId, CancellationToken ct = default);
    Task<IReadOnlyList<SkillAssessmentListItemViewModel>> GetVerifiedSkillsForStudentAsync(Guid studentId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetVerifiedSkillIdsAsync(Guid studentId, CancellationToken ct = default);
    Task<bool> IsSkillVerifiedAsync(Guid studentId, Guid skillId, CancellationToken ct = default);
}
