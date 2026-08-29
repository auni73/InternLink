using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Skills;

public class StudentSkillService : IStudentSkillService
{
    private readonly IAssessmentRepository _assessmentRepository;
    private readonly ILogger<StudentSkillService> _logger;

    public StudentSkillService(
        IAssessmentRepository assessmentRepository,
        ILogger<StudentSkillService> logger)
    {
        _assessmentRepository = assessmentRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SkillAssessmentListItemViewModel>> GetStudentSkillAssessmentsAsync(
        Guid studentId, 
        CancellationToken ct = default)
    {
        return await _assessmentRepository.GetStudentSkillAssessmentsAsync(studentId, ct);
    }

    public async Task<IReadOnlyList<SkillAssessmentListItemViewModel>> GetVerifiedSkillsForStudentAsync(
        Guid studentId, 
        CancellationToken ct = default)
    {
        var allSkills = await _assessmentRepository.GetStudentSkillAssessmentsAsync(studentId, ct);
        return allSkills.Where(s => s.IsVerified).ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetVerifiedSkillIdsAsync(
        Guid studentId, 
        CancellationToken ct = default)
    {
        return await _assessmentRepository.GetVerifiedSkillIdsAsync(studentId, ct);
    }

    public async Task<bool> IsSkillVerifiedAsync(
        Guid studentId, 
        Guid skillId, 
        CancellationToken ct = default)
    {
        return await _assessmentRepository.IsSkillVerifiedAsync(studentId, skillId, ct);
    }
}
