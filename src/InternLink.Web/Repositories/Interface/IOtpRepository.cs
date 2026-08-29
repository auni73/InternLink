using InternLink.Web.Models;

namespace InternLink.Web.Repositories.Interface;

public interface IOtpRepository
{
    Task InsertAsync(OtpCode code, CancellationToken ct = default);
    Task<OtpCode?> FindPendingByUserAsync(Guid userId, CancellationToken ct = default);
    Task ConsumeAsync(Guid id, DateTimeOffset consumedAt, CancellationToken ct = default);
}
