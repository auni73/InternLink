using InternLink.Web.Models;

namespace InternLink.Web.Repositories.Interface;

public interface ISkillRepository
{
    Task<IReadOnlyList<Skill>> GetAllAsync(CancellationToken ct = default);
    Task<Skill?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Skill>> GetSkillsByStudentIdAsync(Guid studentId, CancellationToken ct = default);
}
