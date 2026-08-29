using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Helpers;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.Recommendation;
using InternLink.Web.Services.Resume;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Student.Controllers;

public class JobsController : StudentControllerBase
{
    private readonly IJobRepository _jobRepository;
    private readonly IApplicationRepository _applicationRepository;
    private readonly IResumeRepository _resumeRepository;
    private readonly IResumeService _resumeService;
    private readonly IFtsCapabilityService _ftsCapabilityService;
    private readonly IRecommendationService _recommendationService;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        IJobRepository jobRepository,
        IApplicationRepository applicationRepository,
        IResumeRepository resumeRepository,
        IResumeService resumeService,
        IFtsCapabilityService ftsCapabilityService,
        IRecommendationService recommendationService,
        ILogger<JobsController> logger)
    {
        _jobRepository = jobRepository;
        _applicationRepository = applicationRepository;
        _resumeRepository = resumeRepository;
        _resumeService = resumeService;
        _ftsCapabilityService = ftsCapabilityService;
        _recommendationService = recommendationService;
        _logger = logger;
    }

    /// <summary>Loaded by a separate fetch after the main list renders, so AI latency never blocks browsing.</summary>
    [HttpGet]
    [Route("Student/Jobs/Recommended")]
    public async Task<IActionResult> Recommended(CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound(new { error = "Student profile not found." });
        }

        var result = await _recommendationService.GetRecommendedJobsAsync(studentId.Value, ct);
        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] JobSearchFilter filter, CancellationToken ct)
    {
        filter ??= new JobSearchFilter();
        if (filter.Page < 1) filter.Page = 1;
        if (filter.PageSize < 1 || filter.PageSize > 50) filter.PageSize = 9;

        var studentId = await GetStudentIdAsync(ct);
        var isFtsAvailable = await _ftsCapabilityService.IsFtsAvailableAsync(ct);

        var (items, totalCount) = await _jobRepository.SearchApprovedOpenJobsAsync(
            filter, 
            studentId, 
            isFtsAvailable, 
            ct);

        var viewModel = new JobListViewModel
        {
            Jobs = items,
            Filter = filter,
            TotalCount = totalCount,
            IsFtsFallback = !isFtsAvailable && !string.IsNullOrWhiteSpace(filter.Keyword)
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        var job = await _jobRepository.GetApprovedJobDetailAsync(id, studentId, ct);

        if (job is null)
        {
            return NotFound("Internship posting not found or is no longer active.");
        }

        if (studentId.HasValue)
        {
            var studentResumes = await _resumeService.GetStudentResumesAsync(studentId.Value, ct);
            job.FinalizedResumes = studentResumes.Where(r => r.IsFinalized).ToList();
        }

        return View(job);
    }

    [HttpPost]
    [Route("Student/Jobs/{id:guid}/Apply")]
    public async Task<IActionResult> Apply(Guid id, [FromBody] ApplyJobRequestDto request, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound(new { error = "Student profile not found." });
        }

        if (request == null || request.ResumeId == Guid.Empty)
        {
            return BadRequest(new { error = "Please select a resume to attach." });
        }

        // 1. Verify that the job is approved, open, and not expired
        var job = await _jobRepository.GetApprovedJobDetailAsync(id, studentId, ct);
        if (job is null)
        {
            return NotFound(new { error = "Job posting is not active or has expired." });
        }

        // 2. Pre-check if already applied
        if (job.HasApplied)
        {
            return StatusCode(StatusCodes.Status409Conflict, new { error = "You have already applied to this job." });
        }

        // 3. Verify resume ownership and finalized state
        var resume = await _resumeRepository.GetByIdAsync(request.ResumeId, ct);
        if (resume is null || resume.StudentId != studentId.Value)
        {
            return NotFound(new { error = "Selected resume not found." });
        }

        if (string.IsNullOrWhiteSpace(resume.DocumentPath))
        {
            return BadRequest(new { error = "Please finalize your resume before applying." });
        }

        // 4. Insert application with DB unique constraint guard
        try
        {
            await _applicationRepository.ApplyAsync(
                jobId: id, 
                studentId: studentId.Value, 
                resumeId: request.ResumeId, 
                coverLetter: request.CoverLetterText?.Trim(), 
                ct: ct);

            return Json(new { success = true, message = "Application submitted successfully!" });
        }
        catch (Exception ex) when (DbExceptionMapper.IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning("Duplicate application attempt detected for student {StudentId} on job {JobId}", studentId, id);
            return StatusCode(StatusCodes.Status409Conflict, new { error = "You have already applied to this job." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit application for student {StudentId} on job {JobId}", studentId, id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to submit application. Please try again." });
        }
    }
}
