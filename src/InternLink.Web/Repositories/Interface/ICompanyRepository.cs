using InternLink.Web.Models;

namespace InternLink.Web.Repositories.Interface;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Company?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
