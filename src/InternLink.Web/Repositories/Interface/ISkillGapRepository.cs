using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Interface;

public interface ISkillGapRepository
{
    Task<IReadOnlyList<JobRequiredSkillRow>> GetJobRequiredSkillsAsync(Guid jobId, CancellationToken ct = default);

    Task<IReadOnlyList<StudentHeldSkillRow>> GetStudentHeldSkillsAsync(Guid studentId, CancellationToken ct = default);

    /// <summary>
    /// Resolves the student and job behind an application, but only if the job belongs to the caller's
    /// company. The ownership test is part of the WHERE clause, so a foreign application is unresolvable.
    /// </summary>
    Task<ApplicationSkillGapScope?> GetApplicationScopeAsync(
        Guid applicationId,
        Guid companyId,
        CancellationToken ct = default);
}
