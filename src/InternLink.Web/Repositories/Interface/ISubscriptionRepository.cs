using InternLink.Web.Models;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Interface;

public interface ISubscriptionRepository
{
    Task<IReadOnlyList<SubscriptionPlanDto>> GetActivePlansAsync(CancellationToken ct = default);
    Task<SubscriptionPlan?> GetPlanByIdAsync(int planId, CancellationToken ct = default);
    Task<CompanySubscriptionDto?> GetActiveSubscriptionAsync(Guid companyId, CancellationToken ct = default);
    Task<int> GetActiveJobsCountAsync(Guid companyId, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentTransactionDto>> GetCompanyTransactionHistoryAsync(Guid companyId, CancellationToken ct = default);
    Task<PaymentTransaction?> GetTransactionByInvoiceAsync(string invoiceNumber, CancellationToken ct = default);
    Task<PaymentTransaction?> GetTransactionByBkashPaymentIdAsync(string paymentId, CancellationToken ct = default);
    Task<PaymentTransaction> CreateTransactionAsync(Guid companyId, int planId, string invoiceNumber, decimal amount, string paymentMethod, string? customerRef = null, string? bkashPaymentId = null, CancellationToken ct = default);
    Task<bool> CompletePaymentAndActivateSubscriptionAsync(Guid transactionId, string? bkashTrxId, string? payerMsisdn, DateTimeOffset executeTime, string? rawResponse, CancellationToken ct = default);
    Task<bool> MarkTransactionFailedAsync(Guid transactionId, string? reason, CancellationToken ct = default);
    Task<bool> SubmitManualPaymentRequestAsync(Guid companyId, int planId, string paymentMethod, string referenceNumber, string? customerContact, string? notes, CancellationToken ct = default);
    Task<AdminMonetizationDashboardViewModel> GetAdminDashboardMetricsAsync(CancellationToken ct = default);
    Task<bool> ApproveManualRequestAsync(Guid transactionId, Guid adminUserId, string? notes = null, CancellationToken ct = default);
    Task<bool> RejectManualRequestAsync(Guid transactionId, string reason, CancellationToken ct = default);
    Task<bool> AdminManualGrantAsync(Guid companyId, int planId, int days, Guid adminUserId, string? notes, CancellationToken ct = default);
}
