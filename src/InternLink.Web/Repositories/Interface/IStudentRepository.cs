using InternLink.Web.Models;

namespace InternLink.Web.Repositories.Interface;

public interface IStudentRepository
{
    Task<Student?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Student?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task UpdateProfileAsync(Student student, CancellationToken ct = default);
    Task SyncStudentSkillsAsync(Guid studentId, IEnumerable<(Guid SkillId, int ProficiencyLevel)> skills, CancellationToken ct = default);
    Task<IReadOnlyList<StudentSkill>> GetStudentSkillsAsync(Guid studentId, CancellationToken ct = default);
}
