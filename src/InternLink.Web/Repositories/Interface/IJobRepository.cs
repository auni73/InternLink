using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Interface;

public interface IJobRepository
{
    Task<IReadOnlyList<Job>> GetApprovedOpenJobsAsync(LocationType? locationType, int page, int pageSize, CancellationToken ct = default);
    Task<Job?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<int> GetApprovedOpenJobsCountAsync(LocationType? locationType, CancellationToken ct = default);
    Task<(IReadOnlyList<JobListItemViewModel> Items, int TotalCount)> SearchApprovedOpenJobsAsync(
        JobSearchFilter filter, 
        Guid? studentId, 
        bool isFtsAvailable, 
        CancellationToken ct = default);
    Task<JobDetailViewModel?> GetApprovedJobDetailAsync(Guid id, Guid? studentId, CancellationToken ct = default);
}
