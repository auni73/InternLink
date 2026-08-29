using InternLink.Web.Models;
using InternLink.Web.Models.Enums;

namespace InternLink.Web.Repositories.Interface;

public interface IJobRepository
{
    Task<IReadOnlyList<Job>> GetApprovedOpenJobsAsync(LocationType? locationType, int page, int pageSize, CancellationToken ct = default);
    Task<Job?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<int> GetApprovedOpenJobsCountAsync(LocationType? locationType, CancellationToken ct = default);
}
