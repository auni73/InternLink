using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Counselor.Controllers;

[Area("Counselor")]
[Authorize(Roles = "Counselor,Admin")]
public class StudentsController : Controller
{
    private readonly ICounselorRepository _counselorRepo;
    private readonly IStudentRepository _studentRepo;
    private readonly IResumeRepository _resumeRepo;
    private readonly IApplicationRepository _applicationRepo;
    private readonly IMarkdownService _markdownService;
    private readonly UserManager<AppUser> _userManager;

    public StudentsController(
        ICounselorRepository counselorRepo,
        IStudentRepository studentRepo,
        IResumeRepository resumeRepo,
        IApplicationRepository applicationRepo,
        IMarkdownService markdownService,
        UserManager<AppUser> userManager)
    {
        _counselorRepo = counselorRepo;
        _studentRepo = studentRepo;
        _resumeRepo = resumeRepo;
        _applicationRepo = applicationRepo;
        _markdownService = markdownService;
        _userManager = userManager;
    }

    // GET: /Counselor/Students?search=&page=1&pageSize=15
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 15, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 15;

        var (items, totalCount) = await _counselorRepo.GetStudentDirectoryAsync(search, page, pageSize, ct);

        var viewModel = new CounselorStudentDirectoryViewModel
        {
            Students = items,
            Search = search,
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return View(viewModel);
    }

    // GET: /Counselor/Students/{id}?tab=profile
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, string? tab = "profile", CancellationToken ct = default)
    {
        var student = await _studentRepo.GetByIdAsync(id, ct);
        if (student == null)
        {
            TempData["ErrorMessage"] = "Student profile not found.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(student.UserId.ToString());
        var skills = await _studentRepo.GetStudentSkillsAsync(student.Id, ct);
        var resumes = await _resumeRepo.GetByStudentIdAsync(student.Id, ct);
        var applications = await _applicationRepo.GetStudentApplicationsWithDetailsAsync(student.Id, ct);
        var feedbacks = await _counselorRepo.GetCounselorFeedbacksByStudentIdAsync(student.Id, ct);

        // Pre-render sanitized markdown for the feedback list
        foreach (var feedback in feedbacks)
        {
            feedback.RenderedHtml = _markdownService.RenderToHtml(feedback.NarrativeMarkdown);
        }

        var viewModel = new CounselorStudentDetailViewModel
        {
            Student = student,
            UserEmail = user?.Email ?? "N/A",
            Skills = skills,
            Resumes = resumes,
            Applications = applications,
            Feedbacks = feedbacks,
            ActiveTab = string.IsNullOrWhiteSpace(tab) ? "profile" : tab.ToLowerInvariant(),
            NewFeedback = new CounselorFeedbackCreateViewModel
            {
                StudentId = student.Id,
                MeetingDate = DateTimeOffset.UtcNow
            }
        };

        return View(viewModel);
    }

    // POST: /Counselor/Students/{id}/Feedback
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Feedback(Guid id, CounselorFeedbackCreateViewModel model, CancellationToken ct = default)
    {
        if (id != model.StudentId)
        {
            return BadRequest();
        }

        var student = await _studentRepo.GetByIdAsync(id, ct);
        if (student == null)
        {
            TempData["ErrorMessage"] = "Student not found.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Please correct the errors in your feedback submission.";
            return RedirectToAction(nameof(Details), new { id = model.StudentId, tab = "notes" });
        }

        var counselorUserIdString = _userManager.GetUserId(User);
        if (!Guid.TryParse(counselorUserIdString, out var counselorUserId))
        {
            return Unauthorized();
        }

        await _counselorRepo.AddCounselorFeedbackAsync(
            model.StudentId, 
            counselorUserId, 
            model.NarrativeMarkdown, 
            model.MeetingDate, 
            ct);

        // NOTIFY: Prompt 26 will send notification to student here

        TempData["SuccessMessage"] = "Advising feedback note saved successfully.";
        return RedirectToAction(nameof(Details), new { id = model.StudentId, tab = "notes" });
    }
}
