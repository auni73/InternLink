using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.Jobs;
using InternLink.Web.Services.Vectors;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Admin.Controllers;

public class JobsController : AdminControllerBase
{
    private readonly IAdminModerationRepository _moderationRepo;
    private readonly IJobRepository _jobRepository;
    private readonly ISkillRepository _skillRepository;
    private readonly IJobIndexQueue _indexQueue;
    private readonly IExternalJobIngestionService _ingestionService;
    private readonly IExternalJobParseService _parseService;
    private readonly ICompanyRepository _companyRepository;
    private readonly InternLink.Web.Services.Notification.INotificationService _notificationService;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        IAdminModerationRepository moderationRepo,
        IJobRepository jobRepository,
        ISkillRepository skillRepository,
        IJobIndexQueue indexQueue,
        IExternalJobIngestionService ingestionService,
        IExternalJobParseService parseService,
        ICompanyRepository companyRepository,
        InternLink.Web.Services.Notification.INotificationService notificationService,
        ILogger<JobsController> logger)
    {
        _moderationRepo = moderationRepo;
        _jobRepository = jobRepository;
        _skillRepository = skillRepository;
        _indexQueue = indexQueue;
        _ingestionService = ingestionService;
        _parseService = parseService;
        _companyRepository = companyRepository;
        _notificationService = notificationService;
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

        // NOTIFY: Company user that job posting is live
        try
        {
            var job = await _jobRepository.GetByIdAsync(id, ct);
            if (job?.CompanyId != null)
            {
                var company = await _companyRepository.GetByIdAsync(job.CompanyId.Value, ct);
                if (company != null)
                {
                    await _notificationService.CreateAsync(
                        company.UserId,
                        $"Your job posting '{job.Title}' is now live",
                        "/Company/Jobs",
                        ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification for approved job {JobId}", id);
        }

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

    [HttpPost]
    [Route("Admin/Jobs/External/Sync")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncExternalJobs(CancellationToken ct)
    {
        var result = await _ingestionService.SyncAllSourcesAsync(ct);
        return Json(result);
    }

    [HttpPost]
    [Route("Admin/Jobs/External/Parse")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ParseExternalJobCircular([FromBody] ParseExternalJobRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.RawContent))
        {
            return BadRequest(new { error = "Please provide raw circular text to parse." });
        }

        var parsed = await _parseService.ParseCircularAsync(
            request.RawContent, 
            CurrentUserId, 
            request.SourceName, 
            request.SourceUrl, 
            ct);

        return Json(parsed);
    }

    [HttpPost]
    [Route("Admin/Jobs/External/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExternalJob([FromBody] CreateExternalJobRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { error = "Invalid job data provided. Please check required fields." });
        }

        var externalJobId = !string.IsNullOrWhiteSpace(request.ExternalJobId) 
            ? request.ExternalJobId.Trim() 
            : $"manual-{Guid.NewGuid():N}"[..18];

        if (await _jobRepository.ExistsExternalJobAsync(request.ExternalSourceName.Trim(), externalJobId, ct))
        {
            return StatusCode(StatusCodes.Status409Conflict, new { error = "A job posting with this source and identifier already exists." });
        }

        var allSkills = await _skillRepository.GetAllAsync(ct);
        var skillMap = allSkills.ToDictionary(s => s.SkillName.ToLowerInvariant(), s => s.Id);
        var matchedSkills = new List<(Guid SkillId, int ImportanceWeight)>();

        if (request.SkillNames != null)
        {
            foreach (var name in request.SkillNames)
            {
                if (skillMap.TryGetValue(name.Trim().ToLowerInvariant(), out var sId))
                {
                    matchedSkills.Add((sId, 3));
                }
            }
        }

        var job = new Job
        {
            Id = Guid.NewGuid(),
            CompanyId = null,
            Title = request.Title.Trim(),
            CompanyNameSnapshot = request.CompanyNameSnapshot.Trim(),
            CoreDescription = request.CoreDescription.Trim(),
            SelectionCriteria = request.SelectionCriteria?.Trim() ?? string.Empty,
            LocationType = request.LocationType,
            DeadLine = request.DeadLine > DateTimeOffset.UtcNow ? request.DeadLine : DateTimeOffset.UtcNow.AddDays(30),
            IsApproved = true,
            IsClosed = false,
            Source = JobSource.External,
            ExternalSourceName = request.ExternalSourceName.Trim(),
            ExternalJobId = externalJobId,
            ExternalApplyUrl = request.ExternalApplyUrl.Trim(),
            LastSyncedAt = DateTimeOffset.UtcNow
        };

        var jobId = await _jobRepository.CreateExternalJobAsync(job, matchedSkills, ct);
        _indexQueue.TryEnqueue(new JobIndexCommand(jobId, JobIndexOperation.Upsert));

        _logger.LogInformation("Admin {AdminId} created external job {JobId} from {Source}", CurrentUserId, jobId, request.ExternalSourceName);

        return Json(new { success = true, jobId, message = "External job published successfully and enqueued for AI search." });
    }
}
