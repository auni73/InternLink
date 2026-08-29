using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.AI;
using InternLink.Web.Services.CoverLetter;
using InternLink.Web.Services.Resume;

namespace InternLink.Web.Areas.Student.Controllers;

public class CoverLetterController : StudentControllerBase
{
    private readonly ICoverLetterService _coverLetters;
    private readonly IStudentRepository _students;
    private readonly IJobRepository _jobs;
    private readonly IPdfRenderer _pdfRenderer;
    private readonly ILogger<CoverLetterController> _logger;

    public CoverLetterController(
        ICoverLetterService coverLetters,
        IStudentRepository students,
        IJobRepository jobs,
        IPdfRenderer pdfRenderer,
        ILogger<CoverLetterController> logger)
    {
        _coverLetters = coverLetters;
        _students = students;
        _jobs = jobs;
        _pdfRenderer = pdfRenderer;
        _logger = logger;
    }

    [HttpPost]
    [Route("Student/Jobs/{jobId:guid}/CoverLetter")]
    public async Task<IActionResult> Generate(Guid jobId, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound(new { error = "Student profile not found." });
        }

        try
        {
            var text = await _coverLetters.GenerateAsync(studentId.Value, jobId, ct);
            if (text is null)
            {
                return NotFound(new { error = "This posting is no longer available." });
            }

            _logger.LogInformation("Student {StudentId} generated a cover letter for job {JobId}.", studentId.Value, jobId);
            return Json(new { generatedText = text });
        }
        catch (AiServiceException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    [HttpPost]
    [Route("Student/Jobs/{jobId:guid}/CoverLetter/Save")]
    public async Task<IActionResult> Save(Guid jobId, [FromBody] SaveCoverLetterRequest request, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound(new { error = "Student profile not found." });
        }

        if (request is null || string.IsNullOrWhiteSpace(request.FinalText))
        {
            return BadRequest(new { error = "Cover letter text cannot be empty." });
        }

        var saved = await _coverLetters.SaveToApplicationAsync(studentId.Value, jobId, request.FinalText, ct);
        if (!saved)
        {
            return Conflict(new { error = "Apply to this job first, then your cover letter can be attached to the application." });
        }

        _logger.LogInformation("Student {StudentId} saved a cover letter to their application for job {JobId}.", studentId.Value, jobId);
        return Json(new { success = true, message = "Cover letter saved to your application." });
    }

    /// <summary>Form post rather than fetch, so the browser handles the file download natively.</summary>
    [HttpPost]
    [Route("Student/Jobs/{jobId:guid}/CoverLetter/Download")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Download(Guid jobId, [FromForm] string finalText, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(finalText))
        {
            return BadRequest("Cover letter text cannot be empty.");
        }

        var student = await _students.GetByIdAsync(studentId.Value, ct);
        var job = await _jobs.GetApprovedJobDetailAsync(jobId, studentId.Value, ct);
        if (student is null || job is null)
        {
            return NotFound();
        }

        var applicantName = $"{student.FirstName} {student.LastName}".Trim();
        var pdf = _pdfRenderer.RenderCoverLetterPdf(applicantName, job.Title, job.CompanyName, finalText);

        var fileName = $"CoverLetter-{Sanitize(job.CompanyName)}-{Sanitize(job.Title)}.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    private static string Sanitize(string value)
    {
        var cleaned = new string(value.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "Document" : cleaned;
    }

    public sealed class SaveCoverLetterRequest
    {
        public string FinalText { get; set; } = string.Empty;
    }
}
