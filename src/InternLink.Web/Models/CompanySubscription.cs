using InternLink.Web.Models.Enums;

namespace InternLink.Web.Models;

public class CompanySubscription
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public int PlanId { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public int MaxActiveJobsSnapshot { get; set; }
    public Guid? ApprovedByAdminId { get; set; }
    public string? AdminNotes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Computed / helper properties
    public int DaysRemaining => Math.Max(0, (int)Math.Ceiling((EndDate - DateTimeOffset.UtcNow).TotalDays));
    public bool IsExpired => EndDate <= DateTimeOffset.UtcNow || Status != SubscriptionStatus.Active;
    public bool IsExpiringSoon => !IsExpired && DaysRemaining <= 7;

    // Navigation properties
    public virtual Company Company { get; set; } = null!;
    public virtual SubscriptionPlan Plan { get; set; } = null!;
    public virtual AppUser? ApprovedByAdmin { get; set; }
    public virtual ICollection<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();
}
