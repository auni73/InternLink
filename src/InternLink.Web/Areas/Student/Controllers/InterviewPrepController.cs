using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.InterviewPrep;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Student.Controllers;

public class InterviewPrepController : StudentControllerBase
{
    private readonly IInterviewPrepService _interviewPrep;
    private readonly IStudentRepository _students;
    private readonly IJobRepository _jobs;
    private readonly ILogger<InterviewPrepController> _logger;

    public InterviewPrepController(
        IInterviewPrepService interviewPrep,
        IStudentRepository students,
        IJobRepository jobs,
        ILogger<InterviewPrepController> logger)
    {
        _interviewPrep = interviewPrep;
        _students = students;
        _jobs = jobs;
        _logger = logger;
    }

    [HttpGet]
    [Route("Student/InterviewPrep/Questions")]
    public async Task<IActionResult> Questions(CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound();
        }

        return View(new QuestionBankPageViewModel
        {
            TargetJobs = await GetTargetJobsAsync(studentId.Value, ct),
            DefaultRole = await GetDefaultRoleAsync(studentId.Value, ct)
        });
    }

    [HttpPost]
    [Route("Student/InterviewPrep/Questions")]
    public async Task<IActionResult> GenerateQuestions(
        [FromBody] QuestionBankRequest request,
        CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound(new { error = "Student profile not found." });
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Role))
        {
            return BadRequest(new { error = "Tell us which role you are preparing for." });
        }

        var questions = await _interviewPrep.GenerateQuestionsAsync(studentId.Value, request.Role.Trim(), request.JobId, ct);
        if (questions.Count == 0)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "We could not build a question set just now. Please try again." });
        }

        return Json(new { questions });
    }

    [HttpGet]
    [Route("Student/InterviewPrep/Mock")]
    public async Task<IActionResult> Mock(CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound();
        }

        return View(new MockInterviewLaunchViewModel
        {
            TargetJobs = await GetTargetJobsAsync(studentId.Value, ct),
            DefaultRole = await GetDefaultRoleAsync(studentId.Value, ct),
            RecentSessions = await _interviewPrep.GetRecentSessionsAsync(studentId.Value, 5, ct)
        });
    }

    [HttpPost]
    [Route("Student/InterviewPrep/Sessions")]
    public async Task<IActionResult> StartSession([FromBody] StartSessionRequest request, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound(new { error = "Student profile not found." });
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Role))
        {
            return BadRequest(new { error = "Tell us which role you are interviewing for." });
        }

        var result = await _interviewPrep.StartSessionAsync(studentId.Value, request.Role.Trim(), request.JobId, ct);
        if (result is null)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "The interviewer is unavailable right now. Please try again." });
        }

        _logger.LogInformation("Student {StudentId} opened mock interview {SessionId}.", studentId.Value, result.SessionId);
        return Json(new { sessionId = result.SessionId, firstQuestion = result.FirstQuestion });
    }

    /// <summary>Resumes an interview in place, so a closed tab costs nothing.</summary>
    [HttpGet]
    [Route("Student/InterviewPrep/Mock/{sessionId:guid}")]
    public async Task<IActionResult> Session(Guid sessionId, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound();
        }

        var session = await _interviewPrep.GetSessionAsync(sessionId, studentId.Value, ct);
        if (session is null)
        {
            return NotFound();
        }

        if (session.Status == MockInterviewStatus.Completed)
        {
            return RedirectToAction(nameof(Report), new { sessionId });
        }

        return View(session);
    }

    [HttpPost]
    [Route("Student/InterviewPrep/Sessions/{sessionId:guid}/Message")]
    public async Task<IActionResult> Message(
        Guid sessionId,
        [FromBody] SendMessageRequest request,
        CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound(new { error = "Student profile not found." });
        }

        if (request is null || string.IsNullOrWhiteSpace(request.StudentReply))
        {
            return BadRequest(new { error = "Type an answer before sending." });
        }

        var result = await _interviewPrep.SendMessageAsync(sessionId, studentId.Value, request.StudentReply.Trim(), ct);

        return result.Outcome switch
        {
            // Another student's session is reported as missing rather than forbidden.
            SendMessageOutcome.SessionNotFound => NotFound(new { error = "Interview session not found." }),
            SendMessageOutcome.SessionAlreadyCompleted => Conflict(new { error = "This interview has already ended." }),
            SendMessageOutcome.AiUnavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "The interviewer did not respond. Your answer was saved, so you can send it again." }),
            _ => Json(new { aiReply = result.AiReply })
        };
    }

    [HttpPost]
    [Route("Student/InterviewPrep/Sessions/{sessionId:guid}/End")]
    public async Task<IActionResult> End(Guid sessionId, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound(new { error = "Student profile not found." });
        }

        var report = await _interviewPrep.EndSessionAsync(sessionId, studentId.Value, ct);
        if (report is null)
        {
            return NotFound(new { error = "Interview session not found." });
        }

        _logger.LogInformation("Student {StudentId} ended mock interview {SessionId}.", studentId.Value, sessionId);
        return Json(new
        {
            report.AccuracySummary,
            report.LogicGaps,
            report.ImprovementSuggestions,
            reportUrl = Url.Action(nameof(Report), new { sessionId })
        });
    }

    [HttpGet]
    [Route("Student/InterviewPrep/Mock/{sessionId:guid}/Report")]
    public async Task<IActionResult> Report(Guid sessionId, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound();
        }

        var report = await _interviewPrep.GetReportAsync(sessionId, studentId.Value, ct);
        if (report is null)
        {
            return NotFound();
        }

        return View(report);
    }

    private async Task<IReadOnlyList<TargetJobOption>> GetTargetJobsAsync(Guid studentId, CancellationToken ct)
    {
        var (openJobs, _) = await _jobs.SearchApprovedOpenJobsAsync(
            new JobSearchFilter { Page = 1, PageSize = 20 },
            studentId,
            isFtsAvailable: false,
            ct);

        return openJobs
            .Select(j => new TargetJobOption { JobId = j.Id, Title = j.Title, CompanyName = j.CompanyName })
            .ToList();
    }

    private async Task<string> GetDefaultRoleAsync(Guid studentId, CancellationToken ct)
    {
        var student = await _students.GetByIdAsync(studentId, ct);
        return string.IsNullOrWhiteSpace(student?.Department) ? string.Empty : $"{student.Department} Intern";
    }

    public sealed class QuestionBankRequest
    {
        public string Role { get; set; } = string.Empty;
        public Guid? JobId { get; set; }
    }

    public sealed class StartSessionRequest
    {
        public string Role { get; set; } = string.Empty;
        public Guid? JobId { get; set; }
    }

    public sealed class SendMessageRequest
    {
        public string StudentReply { get; set; } = string.Empty;
    }
}
