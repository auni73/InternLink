using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Payment;

public interface IBkashPaymentService
{
    Task<BkashCreatePaymentResultDto> CreatePaymentAsync(decimal amount, string invoiceNumber, string payerReference, CancellationToken ct = default);
    Task<BkashExecutePaymentResultDto> ExecutePaymentAsync(string paymentId, CancellationToken ct = default);
    Task<BkashExecutePaymentResultDto> QueryPaymentAsync(string paymentId, CancellationToken ct = default);
}
