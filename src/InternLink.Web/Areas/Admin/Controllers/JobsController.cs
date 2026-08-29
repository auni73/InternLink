using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.Vectors;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Admin.Controllers;

public class JobsController : AdminControllerBase
{
    private readonly IAdminModerationRepository _moderationRepo;
    private readonly IJobRepository _jobRepository;
    private readonly IJobIndexQueue _indexQueue;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        IAdminModerationRepository moderationRepo,
        IJobRepository jobRepository,
        IJobIndexQueue indexQueue,
        ILogger<JobsController> logger)
    {
        _moderationRepo = moderationRepo;
        _jobRepository = jobRepository;
        _indexQueue = indexQueue;
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

        // Enqueue only: the embedding call happens on the background indexer, never in this request.
        _indexQueue.TryEnqueue(new JobIndexCommand(id, JobIndexOperation.Upsert));

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

    /// <summary>Reconcile pass: re-enqueues every live posting after a Qdrant reset or first-time backfill.</summary>
    [HttpPost]
    [Route("Admin/Jobs/ReindexAll")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReindexAll(CancellationToken ct)
    {
        var jobIds = await _jobRepository.GetApprovedOpenJobIdsAsync(ct);

        var queued = 0;
        foreach (var jobId in jobIds)
        {
            if (_indexQueue.TryEnqueue(new JobIndexCommand(jobId, JobIndexOperation.Upsert)))
            {
                queued++;
            }
        }

        _logger.LogInformation(
            "Admin {AdminId} ReindexAll queued {Queued} of {Total} live jobs.",
            CurrentUserId,
            queued,
            jobIds.Count);

        var isJsonRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                            Request.Headers.Accept.ToString().Contains("application/json") ||
                            (Request.ContentType?.Contains("application/json") ?? false);

        var message = $"Queued {queued} of {jobIds.Count} live job postings for semantic reindexing.";

        if (isJsonRequest)
        {
            return Json(new { success = true, queued, total = jobIds.Count, message });
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Index));
    }
}
