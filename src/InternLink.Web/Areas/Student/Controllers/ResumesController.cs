using System.IO;
using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.Resume;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Student.Controllers;

public class ResumesController : StudentControllerBase
{
    private readonly IResumeService _resumeService;
    private readonly IResumeRepository _resumeRepository;
    private readonly IResumeAnalysisService _analysisService;
    private readonly IJobRepository _jobRepository;
    private readonly ILogger<ResumesController> _logger;

    public ResumesController(
        IResumeService resumeService,
        IResumeRepository resumeRepository,
        IResumeAnalysisService analysisService,
        IJobRepository jobRepository,
        ILogger<ResumesController> logger)
    {
        _resumeService = resumeService;
        _resumeRepository = resumeRepository;
        _analysisService = analysisService;
        _jobRepository = jobRepository;
        _logger = logger;
    }

    [HttpGet]
    [Route("Student/Resumes/{id:guid}/Analyze")]
    public async Task<IActionResult> Analyze(Guid id, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound();
        }

        var resume = await _resumeService.GetResumeForEditAsync(id, studentId.Value, ct);
        if (resume is null)
        {
            return NotFound("Resume not found.");
        }

        var (openJobs, _) = await _jobRepository.SearchApprovedOpenJobsAsync(
            new JobSearchFilter { Page = 1, PageSize = 20 },
            studentId.Value,
            isFtsAvailable: false,
            ct);

        var viewModel = new ResumeAnalysisPageViewModel
        {
            ResumeId = resume.ResumeId,
            CandidateName = resume.Data.PersonalInfo.FullName,
            IsFinalized = resume.IsFinalized,
            TargetJobs = openJobs
                .Select(j => new TargetJobOption { JobId = j.Id, Title = j.Title, CompanyName = j.CompanyName })
                .ToList()
        };

        return View(viewModel);
    }

    [HttpPost]
    [Route("Student/Resumes/{id:guid}/Analyze")]
    public async Task<IActionResult> Analyze(Guid id, [FromQuery] Guid? targetJobId, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound(new { error = "Student profile not found." });
        }

        var resume = await _resumeService.GetResumeForEditAsync(id, studentId.Value, ct);
        if (resume is null)
        {
            return NotFound(new { error = "Resume not found." });
        }

        var score = await _analysisService.GetAtsScoreAsync(id, studentId.Value, targetJobId, ct);

        var suggestions = targetJobId.HasValue && targetJobId.Value != Guid.Empty
            ? await _analysisService.GetImprovementSuggestionsAsync(id, studentId.Value, targetJobId.Value, ct)
            : [];

        _logger.LogInformation(
            "Student {StudentId} analysed resume {ResumeId} against job {TargetJobId}.",
            studentId.Value,
            id,
            targetJobId);

        return Json(new ResumeAnalysisResultViewModel { Score = score, Suggestions = suggestions });
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound();
        }

        var resumes = await _resumeService.GetStudentResumesAsync(studentId.Value, ct);
        var viewModel = new ResumeListViewModel { Resumes = resumes };
        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Builder(Guid? resumeId, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound();
        }

        ResumeBuilderViewModel? model;
        if (resumeId.HasValue && resumeId.Value != Guid.Empty)
        {
            model = await _resumeService.GetResumeForEditAsync(resumeId.Value, studentId.Value, ct);
            if (model is null)
            {
                return NotFound();
            }
        }
        else
        {
            var newResume = await _resumeService.CreateDraftResumeAsync(studentId.Value, ct);
            model = await _resumeService.GetResumeForEditAsync(newResume.Id, studentId.Value, ct);
            if (model is null)
            {
                return NotFound();
            }
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound(new { error = "Student profile not found." });
        }

        var resume = await _resumeService.CreateDraftResumeAsync(studentId.Value, ct);
        return Json(new { resumeId = resume.Id });
    }

    [HttpPut]
    [Route("Student/Resumes/{id:guid}/Step/{stepName}")]
    public async Task<IActionResult> SaveStep(Guid id, string stepName, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound(new { error = "Student profile not found." });
        }

        using var reader = new StreamReader(Request.Body);
        var stepJson = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(stepJson))
        {
            return BadRequest(new { error = "Step payload cannot be empty." });
        }

        var success = await _resumeService.UpdateStepAsync(id, studentId.Value, stepName, stepJson, ct);
        if (!success)
        {
            return NotFound(new { error = "Resume not found or invalid step." });
        }

        return Json(new { success = true, lastSaved = DateTimeOffset.UtcNow.ToString("O") });
    }

    [HttpPost]
    [Route("Student/Resumes/{id:guid}/Finalize")]
    public async Task<IActionResult> Finalize(Guid id, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound(new { error = "Student profile not found." });
        }

        var (success, docPath, error) = await _resumeService.FinalizeResumeAsync(id, studentId.Value, ct);
        if (!success)
        {
            return BadRequest(new { error = error ?? "Failed to finalize resume." });
        }

        var downloadUrl = Url.Action(nameof(Download), "Resumes", new { area = "Student", id });
        return Json(new { success = true, downloadUrl });
    }

    [HttpGet]
    [Route("Student/Resumes/{id:guid}/Download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound();
        }

        var (stream, fileName) = await _resumeService.OpenDownloadStreamAsync(id, studentId.Value, ct);
        if (stream is null)
        {
            return NotFound();
        }

        return File(stream, "application/pdf", fileName);
    }
}
