using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.Assessment;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Student.Controllers;

public class AssessmentsController : StudentControllerBase
{
    private readonly IAssessmentRepository _assessmentRepository;
    private readonly ISkillRepository _skillRepository;
    private readonly IAssessmentQuestionProvider _questionProvider;
    private readonly IAssessmentSessionService _sessionService;
    private readonly ILogger<AssessmentsController> _logger;

    public AssessmentsController(
        IAssessmentRepository assessmentRepository,
        ISkillRepository skillRepository,
        IAssessmentQuestionProvider questionProvider,
        IAssessmentSessionService sessionService,
        ILogger<AssessmentsController> logger)
    {
        _assessmentRepository = assessmentRepository;
        _skillRepository = skillRepository;
        _questionProvider = questionProvider;
        _sessionService = sessionService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound();
        }

        var skills = await _assessmentRepository.GetStudentSkillAssessmentsAsync(studentId.Value, ct);
        var viewModel = new StudentAssessmentsViewModel
        {
            Skills = skills
        };

        return View(viewModel);
    }

    [HttpGet]
    [Route("Student/Assessments/{skillId:guid}")]
    public async Task<IActionResult> Take(Guid skillId, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound();
        }

        var skill = await _skillRepository.GetByIdAsync(skillId, ct);
        if (skill is null)
        {
            return NotFound("Skill not found.");
        }

        if (!_questionProvider.HasQuestionsForSkill(skill.SkillName))
        {
            TempData["ErrorMessage"] = $"Assessments for '{skill.SkillName}' are not currently available.";
            return RedirectToAction(nameof(Index));
        }

        var questions = _questionProvider.GetExamQuestions(skill.SkillName);
        var token = _sessionService.CreateSessionToken(
            studentId.Value, 
            skill.Id, 
            skill.SkillName, 
            questions.Select(q => q.QuestionId));

        var viewModel = new AssessmentExamViewModel
        {
            SkillId = skill.Id,
            SkillName = skill.SkillName,
            SessionToken = token,
            DurationMinutes = 10,
            Questions = questions
        };

        return View("Take", viewModel);
    }

    [HttpPost]
    [Route("Student/Assessments/Submit")]
    public async Task<IActionResult> Submit([FromBody] AssessmentSubmissionRequestDto request, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return Unauthorized(new { error = "Student profile not found." });
        }

        if (request == null || string.IsNullOrWhiteSpace(request.SessionToken))
        {
            return BadRequest(new { error = "Session token is required." });
        }

        // Server-side timing and token validation
        var (isValid, payload, errorMessage) = _sessionService.ValidateSessionToken(
            request.SessionToken, 
            studentId.Value, 
            request.SkillId);

        if (!isValid || payload == null)
        {
            return BadRequest(new { error = errorMessage ?? "Invalid assessment session." });
        }

        // Evaluate score server-side
        var evaluation = _questionProvider.Evaluate(payload.SkillName, request.Answers);

        // Check if student was already verified for this skill
        var wasAlreadyVerified = await _assessmentRepository.IsSkillVerifiedAsync(studentId.Value, request.SkillId, ct);

        // Persist attempt to database
        await _assessmentRepository.RecordAssessmentResultAsync(
            studentId.Value, 
            request.SkillId, 
            evaluation.AchievedScore, 
            ct);

        _logger.LogInformation(
            "Student {StudentId} completed {Skill} assessment. Score: {Score}%. Passed: {Passed}", 
            studentId.Value, payload.SkillName, evaluation.AchievedScore, evaluation.IsPassed);

        var resultModel = new AssessmentResultViewModel
        {
            SkillId = request.SkillId,
            SkillName = payload.SkillName,
            AchievedScore = evaluation.AchievedScore,
            CorrectCount = evaluation.CorrectCount,
            TotalQuestions = evaluation.TotalQuestions,
            IsPassed = evaluation.IsPassed,
            WasAlreadyVerified = wasAlreadyVerified,
            CompletedAt = DateTimeOffset.UtcNow,
            QuestionFeedback = evaluation.QuestionFeedback
        };

        return Json(new { success = true, result = resultModel });
    }
}
