using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Admin.Controllers;

public class JobsController : AdminControllerBase
{
    private readonly IAdminModerationRepository _moderationRepo;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        IAdminModerationRepository moderationRepo,
        ILogger<JobsController> logger)
    {
        _moderationRepo = moderationRepo;
        _logger = logger;
    }

    [HttpGet]
    [Route("Admin/Jobs")]
    [Route("Admin/Jobs/Index")]
    public async Task<IActionResult> Index(
        bool? approved = false, 
        CancellationToken ct = default)
    {
        var (jobs, pendingCount, approvedCount) = 
            await _moderationRepo.GetJobsQueueAsync(approved, ct);

        var viewModel = new AdminJobQueueViewModel
        {
            ApprovedFilter = approved,
            Jobs = jobs,
            PendingCount = pendingCount,
            ApprovedCount = approvedCount
        };

        return View(viewModel);
    }

    [HttpPost]
    [Route("Admin/Jobs/{id:guid}/Approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var updated = await _moderationRepo.ApproveJobAsync(id, ct);
        if (!updated)
        {
            return NotFound("Job vacancy not found.");
        }

        // Structured audit logging
        _logger.LogInformation("Admin {AdminId} Approve Job {TargetId}", CurrentUserId, id);

        var isJsonRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                            Request.Headers.Accept.ToString().Contains("application/json") ||
                            (Request.ContentType?.Contains("application/json") ?? false);

        if (isJsonRequest)
        {
            return Json(new { success = true, message = "Job vacancy approved and published to student job search." });
        }

        TempData["SuccessMessage"] = "Job vacancy approved and published to student job search.";
        return RedirectToAction(nameof(Index));
    }
}
