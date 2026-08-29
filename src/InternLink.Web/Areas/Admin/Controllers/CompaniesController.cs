using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Admin.Controllers;

public class CompaniesController : AdminControllerBase
{
    private readonly IAdminModerationRepository _moderationRepo;
    private readonly ILogger<CompaniesController> _logger;

    public CompaniesController(
        IAdminModerationRepository moderationRepo,
        ILogger<CompaniesController> logger)
    {
        _moderationRepo = moderationRepo;
        _logger = logger;
    }

    [HttpGet]
    [Route("Admin/Companies")]
    [Route("Admin/Companies/Index")]
    public async Task<IActionResult> Index(
        VerificationStatus? status = VerificationStatus.Pending, 
        CancellationToken ct = default)
    {
        var (companies, pendingCount, verifiedCount, rejectedCount) = 
            await _moderationRepo.GetCompaniesQueueAsync(status, ct);

        var viewModel = new AdminCompanyQueueViewModel
        {
            StatusFilter = status,
            Companies = companies,
            PendingCount = pendingCount,
            VerifiedCount = verifiedCount,
            RejectedCount = rejectedCount
        };

        return View(viewModel);
    }

    [HttpPost]
    [Route("Admin/Companies/{id:guid}/Approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var updated = await _moderationRepo.ApproveCompanyAsync(id, ct);
        if (!updated)
        {
            return NotFound("Company record not found.");
        }

        // Structured audit logging
        _logger.LogInformation("Admin {AdminId} Approve Company {TargetId}", CurrentUserId, id);

        var isJsonRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                            Request.Headers.Accept.ToString().Contains("application/json") ||
                            (Request.ContentType?.Contains("application/json") ?? false);

        if (isJsonRequest)
        {
            return Json(new { success = true, message = "Company verified successfully. Organization can now post and manage job listings." });
        }

        TempData["SuccessMessage"] = "Company verified successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Route("Admin/Companies/{id:guid}/Reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(
        Guid id, 
        [FromForm] string? reason, 
        [FromBody] CompanyRejectRequestDto? jsonRequest, 
        CancellationToken ct)
    {
        var rejectionReason = !string.IsNullOrWhiteSpace(reason) 
            ? reason 
            : jsonRequest?.Reason;

        var updated = await _moderationRepo.RejectCompanyAsync(id, rejectionReason, ct);
        if (!updated)
        {
            return NotFound("Company record not found.");
        }

        // Structured audit logging
        _logger.LogInformation("Admin {AdminId} Reject Company {TargetId}", CurrentUserId, id);

        var isJsonRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                            Request.Headers.Accept.ToString().Contains("application/json") ||
                            (Request.ContentType?.Contains("application/json") ?? false);

        if (isJsonRequest)
        {
            return Json(new { success = true, message = "Company registration marked as rejected." });
        }

        TempData["SuccessMessage"] = "Company registration marked as rejected.";
        return RedirectToAction(nameof(Index));
    }
}
