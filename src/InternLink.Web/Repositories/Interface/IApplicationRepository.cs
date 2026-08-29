using InternLink.Web.Models;

namespace InternLink.Web.Repositories.Interface;

public interface IApplicationRepository
{
    Task<IReadOnlyList<Application>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default);
    Task<Application?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
