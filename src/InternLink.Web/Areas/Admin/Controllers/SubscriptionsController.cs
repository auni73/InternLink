using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.Email;
using InternLink.Web.Services.Notification;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Admin.Controllers;

public class SubscriptionsController : AdminControllerBase
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly INotificationService _notificationService;
    private readonly IEmailSender _emailSender;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(
        ISubscriptionRepository subscriptionRepository,
        ICompanyRepository companyRepository,
        INotificationService notificationService,
        IEmailSender emailSender,
        UserManager<AppUser> userManager,
        ILogger<SubscriptionsController> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _companyRepository = companyRepository;
        _notificationService = notificationService;
        _emailSender = emailSender;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await _subscriptionRepository.GetAdminDashboardMetricsAsync(ct);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRequest(Guid transactionId, string? notes, CancellationToken ct)
    {
        var success = await _subscriptionRepository.ApproveManualRequestAsync(transactionId, CurrentUserId, notes, ct);
        if (!success)
        {
            TempData["ErrorMessage"] = "Could not approve this payment request. It may have already been processed.";
            return RedirectToAction(nameof(Index));
        }

        // Notify employer
        var dashboard = await _subscriptionRepository.GetAdminDashboardMetricsAsync(ct);
        var trx = dashboard.Transactions.FirstOrDefault(t => t.Id == transactionId);
        if (trx != null)
        {
            var company = await _companyRepository.GetByIdAsync(trx.CompanyId, ct);
            if (company != null)
            {
                await _notificationService.CreateAsync(
                    company.UserId,
                    $"Your offline subscription payment of ৳{trx.Amount:N0} for {trx.PlanName} has been approved and activated by an administrator!",
                    "/Company/Subscription",
                    ct);

                try
                {
                    var companyUser = await _userManager.FindByIdAsync(company.UserId.ToString());
                    if (!string.IsNullOrEmpty(companyUser?.Email))
                    {
                        await _emailSender.SendAsync(
                            companyUser.Email,
                            "InternLink: Subscription Payment Approved",
                            $"<h3>Payment Approved</h3><p>Dear {company.CompanyName},</p><p>Your offline payment of <strong>৳{trx.Amount:N0}</strong> (Invoice: {trx.InvoiceNumber}) has been approved by our billing department.</p><p>Your <strong>{trx.PlanName}</strong> subscription is now active.</p>",
                            ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send subscription approval email to company {CompanyId}", company.Id);
                }
            }
        }

        TempData["SuccessMessage"] = "Payment verified and company subscription activated successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRequest(AdminRejectRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            TempData["ErrorMessage"] = "Rejection reason is required.";
            return RedirectToAction(nameof(Index));
        }

        var success = await _subscriptionRepository.RejectManualRequestAsync(request.TransactionId, request.Reason, ct);
        if (!success)
        {
            TempData["ErrorMessage"] = "Could not reject this payment request.";
            return RedirectToAction(nameof(Index));
        }

        var dashboard = await _subscriptionRepository.GetAdminDashboardMetricsAsync(ct);
        var trx = dashboard.Transactions.FirstOrDefault(t => t.Id == request.TransactionId);
        if (trx != null)
        {
            var company = await _companyRepository.GetByIdAsync(trx.CompanyId, ct);
            if (company != null)
            {
                await _notificationService.CreateAsync(
                    company.UserId,
                    $"Your subscription payment request (Invoice: {trx.InvoiceNumber}) could not be verified: {request.Reason}",
                    "/Company/Subscription",
                    ct);
            }
        }

        TempData["SuccessMessage"] = "Payment request marked as rejected and company notified.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManualGrant(AdminManualGrantDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Please provide valid grant parameters.";
            return RedirectToAction(nameof(Index));
        }

        var success = await _subscriptionRepository.AdminManualGrantAsync(
            request.CompanyId, request.PlanId, request.Days, CurrentUserId, request.Notes, ct);

        if (!success)
        {
            TempData["ErrorMessage"] = "Could not execute manual plan grant.";
            return RedirectToAction(nameof(Index));
        }

        var company = await _companyRepository.GetByIdAsync(request.CompanyId, ct);
        if (company != null)
        {
            await _notificationService.CreateAsync(
                company.UserId,
                $"An administrator has granted your organization {request.Days} days of complimentary subscription access!",
                "/Company/Subscription",
                ct);
        }

        TempData["SuccessMessage"] = $"Complimentary subscription granted for {request.Days} days successfully!";
        return RedirectToAction(nameof(Index));
    }
}
