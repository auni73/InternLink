using InternLink.Web.Models.Enums;

namespace InternLink.Web.Models;

public class PaymentTransaction
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public int PlanId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "bKash"; // 'bKash', 'BankTransfer', 'AdminGrant'
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BDT";
    public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Initiated;
    public string? BkashPaymentId { get; set; }
    public string? BkashTrxId { get; set; }
    public string? PayerMsisdn { get; set; }
    public string? CustomerReference { get; set; }
    public DateTimeOffset? PaymentExecuteTime { get; set; }
    public string? RawGatewayResponse { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual Company Company { get; set; } = null!;
    public virtual SubscriptionPlan Plan { get; set; } = null!;
    public virtual CompanySubscription? Subscription { get; set; }
}
