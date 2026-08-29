using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Helpers;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Company.Controllers;

[Area("Company")]
public class AtsController : CompanyControllerBase
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly ILogger<AtsController> _logger;

    public AtsController(
        IApplicationRepository applicationRepository,
        ILogger<AtsController> logger)
    {
        _applicationRepository = applicationRepository;
        _logger = logger;
    }

    [HttpGet]
    [Route("Company/Ats")]
    [Route("Company/Ats/Index")]
    [Route("Company/Pipeline")]
    public async Task<IActionResult> Index(Guid? jobId, CancellationToken ct)
    {
        var companyId = await GetCompanyIdAsync(ct);
        if (companyId is null)
        {
            return NotFound("Company profile not found.");
        }

        var companyJobs = await _applicationRepository.GetCompanyJobFilterOptionsAsync(companyId.Value, ct);

        // Verify that requested jobId belongs to this company
        if (jobId.HasValue && jobId.Value != Guid.Empty)
        {
            if (!companyJobs.Any(j => j.JobId == jobId.Value))
            {
                jobId = null;
            }
        }

        var applications = await _applicationRepository.GetCompanyAtsApplicationsAsync(companyId.Value, jobId, ct);

        var viewModel = new CompanyAtsBoardViewModel
        {
            SelectedJobId = jobId,
            CompanyJobs = companyJobs,
            Applications = applications
        };

        return View(viewModel);
    }

    [HttpPut]
    [Route("Company/Ats/Applications/{id:guid}/Status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id, 
        [FromBody] AdvanceStatusRequestDto request, 
        CancellationToken ct)
    {
        var companyId = await GetCompanyIdAsync(ct);
        if (companyId is null)
        {
            return Unauthorized(new { error = "Company profile not found." });
        }

        if (request == null || string.IsNullOrWhiteSpace(request.NewStatus))
        {
            return BadRequest(new { error = "Target status is required." });
        }

        if (!Enum.TryParse<ApplicationStatus>(request.NewStatus.Trim(), true, out var newStatus))
        {
            return BadRequest(new { error = $"Invalid application status '{request.NewStatus}'." });
        }

        var (success, errorMessage) = await _applicationRepository.TransitionApplicationStatusAsync(
            id, 
            companyId.Value, 
            newStatus, 
            request.ScheduledDateTime, 
            request.ContextMeetingLink, 
            ct);

        if (!success)
        {
            return BadRequest(new { error = errorMessage ?? "Invalid status transition." });
        }

        _logger.LogInformation("Company {CompanyId} transitioned application {AppId} to {Status}", companyId.Value, id, newStatus);

        return Json(new AdvanceStatusResponseDto
        {
            Success = true,
            ApplicationId = id,
            NewStatus = newStatus.ToString(),
            StatusBadgeClass = newStatus.GetBadgeClass(),
            StatusBadgeText = newStatus.GetShortLabel(),
            InterviewDateTime = request.ScheduledDateTime?.ToString("MMM dd, yyyy 'at' hh:mm tt 'UTC'"),
            MeetingLink = request.ContextMeetingLink,
            Message = $"Candidate status advanced to {newStatus.GetShortLabel()} successfully."
        });
    }

    [HttpGet]
    [Route("Company/Ats/Applications/{id:guid}")]
    public async Task<IActionResult> GetApplicationDetail(Guid id, CancellationToken ct)
    {
        var companyId = await GetCompanyIdAsync(ct);
        if (companyId is null)
        {
            return NotFound(new { error = "Company profile not found." });
        }

        var detail = await _applicationRepository.GetCompanyApplicationDetailAsync(id, companyId.Value, ct);
        if (detail is null)
        {
            return NotFound(new { error = "Application not found or you do not have permission to access it." });
        }

        return Json(detail);
    }

    [HttpGet]
    [Route("Company/Ats/Applications/{id:guid}/Resume")]
    public async Task<IActionResult> DownloadResume(Guid id, CancellationToken ct)
    {
        var companyId = await GetCompanyIdAsync(ct);
        if (companyId is null)
        {
            return NotFound("Company profile not found.");
        }

        var (stream, fileName) = await _applicationRepository.OpenAuthorizedResumeStreamForCompanyAsync(id, companyId.Value, ct);
        if (stream is null)
        {
            return NotFound("Candidate resume document is not available or access was denied.");
        }

        return File(stream, "application/pdf", fileName);
    }
}
