namespace InternLink.Web.Models;

public class SubscriptionPlan
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int BillingCycleDays { get; set; } = 30;
    public int MaxActiveJobs { get; set; } = 3;
    public string FeaturesJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual ICollection<CompanySubscription> Subscriptions { get; set; } = new List<CompanySubscription>();
    public virtual ICollection<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();
}
