using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Services.Vectors;
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

    // Company Job Management (Single query with COUNT subquery, atomic transactions)
    Task<IReadOnlyList<CompanyJobListItemViewModel>> GetCompanyJobsAsync(Guid companyId, CancellationToken ct = default);
    Task<CompanyJobEditViewModel?> GetCompanyJobForEditAsync(Guid jobId, Guid companyId, CancellationToken ct = default);
    Task<Guid> CreateJobWithSkillsAsync(Guid companyId, CompanyJobEditViewModel model, CancellationToken ct = default);
    Task<bool> UpdateJobWithSkillsAsync(Guid jobId, Guid companyId, CompanyJobEditViewModel model, CancellationToken ct = default);
    Task<bool> CloseJobAsync(Guid jobId, Guid companyId, CancellationToken ct = default);

    // Vector indexing support
    Task<JobVectorSource?> GetJobVectorSourceAsync(Guid jobId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetApprovedOpenJobIdsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetAllJobIdsByCompanyUserIdAsync(Guid companyUserId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetIndexableJobIdsByCompanyUserIdAsync(Guid companyUserId, CancellationToken ct = default);
}
