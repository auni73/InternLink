using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Interface;

public interface IAssessmentRepository
{
    Task<IReadOnlyList<SkillAssessmentListItemViewModel>> GetStudentSkillAssessmentsAsync(Guid studentId, CancellationToken ct = default);
    Task<bool> IsSkillVerifiedAsync(Guid studentId, Guid skillId, CancellationToken ct = default);
    Task RecordAssessmentResultAsync(Guid studentId, Guid skillId, int achievedScore, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetVerifiedSkillIdsAsync(Guid studentId, CancellationToken ct = default);
}
