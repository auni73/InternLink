using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Student.Controllers;

[Area("Student")]
[Authorize(Roles = "Student")]
public class AdvisingNotesController : Controller
{
    private readonly ICounselorRepository _counselorRepo;
    private readonly IStudentRepository _studentRepo;
    private readonly IMarkdownService _markdownService;
    private readonly UserManager<AppUser> _userManager;

    public AdvisingNotesController(
        ICounselorRepository counselorRepo,
        IStudentRepository studentRepo,
        IMarkdownService markdownService,
        UserManager<AppUser> userManager)
    {
        _counselorRepo = counselorRepo;
        _studentRepo = studentRepo;
        _markdownService = markdownService;
        _userManager = userManager;
    }

    // GET: /Student/AdvisingNotes
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var userIdString = _userManager.GetUserId(User);
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        var student = await _studentRepo.GetByUserIdAsync(userId, ct);
        if (student == null)
        {
            TempData["ErrorMessage"] = "Student profile not found. Please complete your profile first.";
            return RedirectToAction("Index", "Profile", new { area = "Student" });
        }

        // Student-facing query scoped strictly to CurrentUserId
        var feedbacks = await _counselorRepo.GetAdvisingNotesForStudentUserAsync(userId, ct);

        foreach (var item in feedbacks)
        {
            item.RenderedHtml = _markdownService.RenderToHtml(item.NarrativeMarkdown);
        }

        var viewModel = new StudentAdvisingNotesViewModel
        {
            StudentId = student.Id,
            StudentName = $"{student.FirstName} {student.LastName}".Trim(),
            Feedbacks = feedbacks
        };

        return View(viewModel);
    }
}
