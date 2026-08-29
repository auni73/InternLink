using InternLink.Web.Models;
using InternLink.Web.Models.Enums;

namespace InternLink.Web.Repositories.Interface;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Company?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task UpdateProfileAsync(Company company, CancellationToken ct = default);
    Task<VerificationStatus?> GetVerificationStatusAsync(Guid companyId, CancellationToken ct = default);
}
