using InternLink.Web.Models.Enums;

namespace InternLink.Web.ViewModels;

// =========================================================================
// 1. User Moderation ViewModels
// =========================================================================

public class AdminUserListViewModel
{
    public string? RoleFilter { get; set; } // "Students", "Companies", or null (All)
    public string? SearchQuery { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int TotalCount { get; set; }
    public IReadOnlyList<AdminUserItemViewModel> Users { get; set; } = Array.Empty<AdminUserItemViewModel>();

    public int TotalStudentsCount { get; set; }
    public int TotalCompaniesCount { get; set; }
    public int TotalAllCount { get; set; }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public class AdminUserItemViewModel
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? DetailSummary { get; set; }
}

// =========================================================================
// 2. Company Verification Queue ViewModels
// =========================================================================

public class AdminCompanyQueueViewModel
{
    public VerificationStatus? StatusFilter { get; set; } = VerificationStatus.Pending;
    public IReadOnlyList<AdminCompanyQueueItemViewModel> Companies { get; set; } = Array.Empty<AdminCompanyQueueItemViewModel>();

    public int PendingCount { get; set; }
    public int VerifiedCount { get; set; }
    public int RejectedCount { get; set; }
    public int TotalCount => PendingCount + VerifiedCount + RejectedCount;
}

public class AdminCompanyQueueItemViewModel
{
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? CorporateWebsite { get; set; }
    public string IndustrySector { get; set; } = string.Empty;
    public VerificationStatus VerificationStatus { get; set; }
    public string? AdminRejectionReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int JobCount { get; set; }
}

// =========================================================================
// 3. Job Approval Queue ViewModels
// =========================================================================

public class AdminJobQueueViewModel
{
    public bool? ApprovedFilter { get; set; } = false; // false: Pending, true: Approved, null: All
    public IReadOnlyList<AdminJobQueueItemViewModel> Jobs { get; set; } = Array.Empty<AdminJobQueueItemViewModel>();

    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int TotalCount => PendingCount + ApprovedCount;
}

public class AdminJobQueueItemViewModel
{
    public Guid JobId { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CorporateWebsite { get; set; }
    public string Title { get; set; } = string.Empty;
    public LocationType LocationType { get; set; }
    public DateTimeOffset DeadLine { get; set; }
    public bool IsApproved { get; set; }
    public bool IsClosed { get; set; }
    public string CoreDescription { get; set; } = string.Empty;
    public string? SelectionCriteria { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int ApplicantCount { get; set; }
}

// =========================================================================
// 4. Moderation Request DTOs
// =========================================================================

public class CompanyRejectRequestDto
{
    public string? Reason { get; set; }
}
