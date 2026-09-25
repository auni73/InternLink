using System.ComponentModel.DataAnnotations;
using InternLink.Web.Models.Enums;

namespace InternLink.Web.ViewModels;

public class SubscriptionPlanDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int BillingCycleDays { get; set; }
    public int MaxActiveJobs { get; set; }
    public List<string> Features { get; set; } = new();
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }

    public string FormattedPrice => Price == 0 ? "Free" : $"৳{Price:N0}";
    public string FormattedCycle => BillingCycleDays == 365 ? "Yearly" : BillingCycleDays == 90 ? "Quarterly" : $"{BillingCycleDays} Days";
    public string FormattedJobSlots => MaxActiveJobs >= 900 ? "Unlimited Vacancies" : $"{MaxActiveJobs} Active Vacancies";
}

public class CompanySubscriptionDto
{
    public Guid Id { get; set; }
    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public SubscriptionStatus Status { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public int MaxActiveJobs { get; set; }
    public int DaysRemaining { get; set; }
    public bool IsExpired { get; set; }
    public bool IsExpiringSoon { get; set; }

    public string StatusBadgeClass => Status switch
    {
        SubscriptionStatus.Active when DaysRemaining > 7 => "bg-success-subtle text-success border border-success-subtle",
        SubscriptionStatus.Active => "bg-warning-subtle text-warning-emphasis border border-warning-subtle",
        SubscriptionStatus.PendingPayment => "bg-info-subtle text-info-emphasis border border-info-subtle",
        SubscriptionStatus.Expired => "bg-danger-subtle text-danger border border-danger-subtle",
        SubscriptionStatus.Suspended => "bg-secondary-subtle text-secondary border border-secondary-subtle",
        _ => "bg-light text-muted border"
    };

    public string StatusLabel => Status switch
    {
        SubscriptionStatus.Active when DaysRemaining > 7 => "Active",
        SubscriptionStatus.Active => "Expiring Soon",
        SubscriptionStatus.PendingPayment => "Pending Approval",
        SubscriptionStatus.Expired => "Expired",
        SubscriptionStatus.Suspended => "Suspended",
        _ => Status.ToString()
    };
}

public class PaymentTransactionDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BDT";
    public PaymentTransactionStatus Status { get; set; }
    public string? BkashPaymentId { get; set; }
    public string? BkashTrxId { get; set; }
    public string? PayerMsisdn { get; set; }
    public string? CustomerReference { get; set; }
    public DateTimeOffset? PaymentExecuteTime { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public string StatusBadgeClass => Status switch
    {
        PaymentTransactionStatus.Completed => "bg-success-subtle text-success border border-success-subtle",
        PaymentTransactionStatus.Initiated => "bg-warning-subtle text-warning-emphasis border border-warning-subtle",
        PaymentTransactionStatus.Failed => "bg-danger-subtle text-danger border border-danger-subtle",
        PaymentTransactionStatus.Cancelled => "bg-secondary-subtle text-secondary border border-secondary-subtle",
        _ => "bg-light text-muted border"
    };
}

public class CompanySubscriptionDashboardViewModel
{
    public CompanySubscriptionDto? CurrentSubscription { get; set; }
    public int ActiveJobsCount { get; set; }
    public int MaxActiveJobs => CurrentSubscription?.MaxActiveJobs ?? 0;
    public bool HasActiveSubscription => CurrentSubscription != null && !CurrentSubscription.IsExpired && CurrentSubscription.Status == SubscriptionStatus.Active;
    public bool CanPostNewJob => HasActiveSubscription && (MaxActiveJobs >= 900 || ActiveJobsCount < MaxActiveJobs);

    public IReadOnlyList<SubscriptionPlanDto> AvailablePlans { get; set; } = Array.Empty<SubscriptionPlanDto>();
    public IReadOnlyList<PaymentTransactionDto> PaymentHistory { get; set; } = Array.Empty<PaymentTransactionDto>();
}

public class SubmitManualPaymentRequestDto
{
    [Required]
    public int PlanId { get; set; }

    [Required]
    [MaxLength(50)]
    public string PaymentMethod { get; set; } = "bKash Send Money";

    [Required]
    [MaxLength(100)]
    [Display(Name = "Transaction ID / Deposit Ref")]
    public string ReferenceNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    [Display(Name = "Sender Phone / Account")]
    public string? CustomerPhoneOrEmail { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class AdminMonetizationDashboardViewModel
{
    public decimal TotalRevenueBdt { get; set; }
    public int ActiveSubscriptionsCount { get; set; }
    public int ExpiringSoonCount { get; set; }
    public int PendingApprovalsCount { get; set; }

    public IReadOnlyList<AdminPaymentRequestRowDto> PendingRequests { get; set; } = Array.Empty<AdminPaymentRequestRowDto>();
    public IReadOnlyList<AdminCompanySubscriptionRowDto> CompanySubscriptions { get; set; } = Array.Empty<AdminCompanySubscriptionRowDto>();
    public IReadOnlyList<PaymentTransactionDto> Transactions { get; set; } = Array.Empty<PaymentTransactionDto>();
    public IReadOnlyList<SubscriptionPlanDto> Plans { get; set; } = Array.Empty<SubscriptionPlanDto>();
}

public class AdminPaymentRequestRowDto
{
    public Guid TransactionId { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? CustomerReference { get; set; }
    public string? PayerMsisdn { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class AdminCompanySubscriptionRowDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string IndustrySector { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string PlanName { get; set; } = "None / Inactive";
    public SubscriptionStatus? Status { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public int DaysRemaining { get; set; }
    public int ActiveJobsCount { get; set; }
    public int MaxActiveJobs { get; set; }
    public decimal TotalPaidBdt { get; set; }
}

public class AdminManualGrantDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public int PlanId { get; set; }

    [Range(1, 1000)]
    public int Days { get; set; } = 30;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class AdminRejectRequestDto
{
    [Required]
    public Guid TransactionId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Reason { get; set; } = string.Empty;
}

public class BkashCreatePaymentResultDto
{
    public bool IsSuccess { get; set; }
    public string? PaymentId { get; set; }
    public string? BkashUrl { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }
}

public class BkashExecutePaymentResultDto
{
    public bool IsSuccess { get; set; }
    public string? PaymentId { get; set; }
    public string? TrxId { get; set; }
    public string? TransactionStatus { get; set; }
    public string? Amount { get; set; }
    public string? CustomerMsisdn { get; set; }
    public DateTimeOffset? ExecuteTime { get; set; }
    public string? ErrorMessage { get; set; }
}
