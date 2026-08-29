using InternLink.Web.Models;

namespace InternLink.Web.Repositories.Interface;

public interface IStudentRepository
{
    Task<Student?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Student?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
