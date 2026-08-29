using InternLink.Web.Models.Enums;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Interface;

public interface IAdminModerationRepository
{
    // User Moderation
    Task<(IReadOnlyList<AdminUserItemViewModel> Users, int TotalCount, int StudentCount, int CompanyCount, int TotalAllCount)> GetUsersAsync(
        string? roleFilter, 
        string? searchQuery, 
        int page, 
        int pageSize, 
        CancellationToken ct = default);

    Task<bool> SetUserActiveStatusAsync(Guid userId, bool isActive, CancellationToken ct = default);

    // Company Verification Queue
    Task<(IReadOnlyList<AdminCompanyQueueItemViewModel> Companies, int PendingCount, int VerifiedCount, int RejectedCount)> GetCompaniesQueueAsync(
        VerificationStatus? statusFilter, 
        CancellationToken ct = default);

    Task<bool> ApproveCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<bool> RejectCompanyAsync(Guid companyId, string? reason, CancellationToken ct = default);

    // Job Approval Queue
    Task<(IReadOnlyList<AdminJobQueueItemViewModel> Jobs, int PendingCount, int ApprovedCount)> GetJobsQueueAsync(
        bool? approvedFilter, 
        CancellationToken ct = default);

    Task<bool> ApproveJobAsync(Guid jobId, CancellationToken ct = default);
}
