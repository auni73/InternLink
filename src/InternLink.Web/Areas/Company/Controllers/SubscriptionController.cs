using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Filters;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.Email;
using InternLink.Web.Services.Notification;
using InternLink.Web.Services.Payment;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Company.Controllers;

public class SubscriptionController : CompanyControllerBase
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IBkashPaymentService _bkashService;
    private readonly ICompanyRepository _companyRepository;
    private readonly INotificationService _notificationService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<SubscriptionController> _logger;

    public SubscriptionController(
        ISubscriptionRepository subscriptionRepository,
        IBkashPaymentService bkashService,
        ICompanyRepository companyRepository,
        INotificationService notificationService,
        IEmailSender emailSender,
        ILogger<SubscriptionController> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _bkashService = bkashService;
        _companyRepository = companyRepository;
        _notificationService = notificationService;
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var companyId = await GetCompanyIdAsync(ct);
        if (companyId is null) return NotFound("Company profile not found.");

        var currentSub = await _subscriptionRepository.GetActiveSubscriptionAsync(companyId.Value, ct);
        var activeJobsCount = await _subscriptionRepository.GetActiveJobsCountAsync(companyId.Value, ct);
        var plans = await _subscriptionRepository.GetActivePlansAsync(ct);
        var history = await _subscriptionRepository.GetCompanyTransactionHistoryAsync(companyId.Value, ct);

        var viewModel = new CompanySubscriptionDashboardViewModel
        {
            CurrentSubscription = currentSub,
            ActiveJobsCount = activeJobsCount,
            AvailablePlans = plans,
            PaymentHistory = history
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PayBkash(int planId, CancellationToken ct)
    {
        var companyId = await GetCompanyIdAsync(ct);
        if (companyId is null) return NotFound("Company profile not found.");

        var plan = await _subscriptionRepository.GetPlanByIdAsync(planId, ct);
        if (plan is null || !plan.IsActive)
        {
            TempData["ErrorMessage"] = "Selected subscription plan is invalid or no longer active.";
            return RedirectToAction(nameof(Index));
        }

        var company = await _companyRepository.GetByIdAsync(companyId.Value, ct);
        var companyName = company?.CompanyName ?? "Company";

        // Free plan instant activation
        if (plan.Price == 0)
        {
            var invoiceNo = $"INV-FREE-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            var trx = await _subscriptionRepository.CreateTransactionAsync(
                companyId.Value, plan.Id, invoiceNo, 0, "FreeTrial", "University Welcome Trial", null, ct);

            await _subscriptionRepository.CompletePaymentAndActivateSubscriptionAsync(
                trx.Id, "FREE-TRIAL", "System", DateTimeOffset.UtcNow, "Free Trial Activation", ct);

            await _notificationService.CreateAsync(
                CurrentUserId, 
                $"Your {plan.Name} has been activated! You have {plan.BillingCycleDays} days to post internships.", 
                "/Company/Subscription", 
                ct);

            TempData["SuccessMessage"] = $"{plan.Name} activated successfully! Enjoy your trial period.";
            return RedirectToAction(nameof(Index));
        }

        var invoiceNumber = $"INV-{DateTime.UtcNow:yyMMddHHmm}-{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}";
        var paymentResult = await _bkashService.CreatePaymentAsync(plan.Price, invoiceNumber, companyName, ct);

        if (!paymentResult.IsSuccess || string.IsNullOrEmpty(paymentResult.BkashUrl))
        {
            TempData["ErrorMessage"] = paymentResult.ErrorMessage ?? "Could not initiate bKash payment gateway. Please try again or submit offline transfer.";
            return RedirectToAction(nameof(Index));
        }

        // Record initiated transaction in SQL Server
        await _subscriptionRepository.CreateTransactionAsync(
            companyId.Value, 
            plan.Id, 
            invoiceNumber, 
            plan.Price, 
            "bKash", 
            null, 
            paymentResult.PaymentId, 
            ct);

        return Redirect(paymentResult.BkashUrl);
    }

    [HttpGet]
    public async Task<IActionResult> BkashCallback(
        [FromQuery] string? paymentID, 
        [FromQuery] string? status, 
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(paymentID))
        {
            TempData["ErrorMessage"] = "Invalid payment callback from bKash.";
            return RedirectToAction(nameof(Index));
        }

        var trx = await _subscriptionRepository.GetTransactionByBkashPaymentIdAsync(paymentID, ct);
        if (trx is null)
        {
            TempData["ErrorMessage"] = "Transaction record not found for this bKash payment.";
            return RedirectToAction(nameof(Index));
        }

        if (status?.ToLowerInvariant() != "success")
        {
            await _subscriptionRepository.MarkTransactionFailedAsync(trx.Id, $"Payment cancelled or failed with status: {status}", ct);
            TempData["ErrorMessage"] = $"bKash payment was {status ?? "cancelled"}. Your account has not been charged.";
            return RedirectToAction(nameof(Index));
        }

        // Execute payment with bKash
        var executeResult = await _bkashService.ExecutePaymentAsync(paymentID, ct);
        if (!executeResult.IsSuccess || string.IsNullOrEmpty(executeResult.TrxId))
        {
            await _subscriptionRepository.MarkTransactionFailedAsync(trx.Id, executeResult.ErrorMessage, ct);
            TempData["ErrorMessage"] = executeResult.ErrorMessage ?? "bKash payment execution was unsuccessful. Please contact support.";
            return RedirectToAction(nameof(Index));
        }

        // Complete in SQL Server and activate subscription
        await _subscriptionRepository.CompletePaymentAndActivateSubscriptionAsync(
            trx.Id,
            executeResult.TrxId,
            executeResult.CustomerMsisdn,
            executeResult.ExecuteTime ?? DateTimeOffset.UtcNow,
            $"bKash Status: {executeResult.TransactionStatus}",
            ct);

        // Notify employer
        await _notificationService.CreateAsync(
            CurrentUserId,
            $"Payment of ৳{trx.Amount:N0} received successfully via bKash (TrxID: {executeResult.TrxId}). Subscription is active!",
            $"/Company/Subscription/Receipt?invoiceNumber={trx.InvoiceNumber}",
            ct);

        TempData["SuccessMessage"] = $"Payment of ৳{trx.Amount:N0} completed successfully! TrxID: {executeResult.TrxId}.";
        return RedirectToAction(nameof(Receipt), new { invoiceNumber = trx.InvoiceNumber });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitManualPayment(SubmitManualPaymentRequestDto request, CancellationToken ct)
    {
        var companyId = await GetCompanyIdAsync(ct);
        if (companyId is null) return NotFound("Company profile not found.");

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Please fill in all required payment reference details.";
            return RedirectToAction(nameof(Index));
        }

        var plan = await _subscriptionRepository.GetPlanByIdAsync(request.PlanId, ct);
        if (plan is null || !plan.IsActive)
        {
            TempData["ErrorMessage"] = "Selected subscription plan is invalid.";
            return RedirectToAction(nameof(Index));
        }

        var success = await _subscriptionRepository.SubmitManualPaymentRequestAsync(
            companyId.Value,
            plan.Id,
            request.PaymentMethod,
            request.ReferenceNumber,
            request.CustomerPhoneOrEmail,
            request.Notes,
            ct);

        if (success)
        {
            await _notificationService.CreateAsync(
                CurrentUserId,
                $"Your offline payment verification request (Ref: {request.ReferenceNumber}) was received. An administrator will verify and activate your subscription shortly.",
                "/Company/Subscription",
                ct);

            TempData["SuccessMessage"] = "Payment verification submitted! Our accounts team will review and approve your subscription shortly.";
        }
        else
        {
            TempData["ErrorMessage"] = "Could not submit payment verification request. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Receipt(string invoiceNumber, CancellationToken ct)
    {
        var companyId = await GetCompanyIdAsync(ct);
        if (companyId is null) return NotFound("Company profile not found.");

        var history = await _subscriptionRepository.GetCompanyTransactionHistoryAsync(companyId.Value, ct);
        var transaction = history.FirstOrDefault(t => string.Equals(t.InvoiceNumber, invoiceNumber, StringComparison.OrdinalIgnoreCase));

        if (transaction is null)
        {
            TempData["ErrorMessage"] = "Invoice not found or you do not have permission to view it.";
            return RedirectToAction(nameof(Index));
        }

        var currentSub = await _subscriptionRepository.GetActiveSubscriptionAsync(companyId.Value, ct);
        ViewBag.CurrentSubscription = currentSub;

        return View(transaction);
    }

    [HttpGet]
    public async Task<IActionResult> SimulatedBkash([FromQuery] string paymentId, CancellationToken ct)
    {
        var trx = await _subscriptionRepository.GetTransactionByBkashPaymentIdAsync(paymentId, ct);
        if (trx is null)
        {
            return NotFound("Transaction not found for this simulated checkout.");
        }

        ViewBag.PaymentId = paymentId;
        ViewBag.Amount = trx.Amount;
        ViewBag.InvoiceNumber = trx.InvoiceNumber;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExecuteSimulatedBkash(
        [FromForm] string paymentId,
        [FromForm] string walletNumber,
        [FromForm] string otp,
        [FromForm] string pin,
        CancellationToken ct)
    {
        // Redirect to standard callback to reuse the single-responsibility verification flow
        return await BkashCallback(paymentId, "success", ct);
    }
}
