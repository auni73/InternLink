using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Student.Controllers;

public class ProfileController : StudentControllerBase
{
    private readonly IStudentRepository _studentRepository;
    private readonly IAssessmentRepository _assessmentRepository;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        IStudentRepository studentRepository, 
        IAssessmentRepository assessmentRepository,
        ILogger<ProfileController> logger)
    {
        _studentRepository = studentRepository;
        _assessmentRepository = assessmentRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound("Student profile not found.");
        }

        var student = await _studentRepository.GetByIdAsync(studentId.Value, ct);
        if (student is null)
        {
            return NotFound("Student profile not found.");
        }

        var skills = await _studentRepository.GetStudentSkillsAsync(studentId.Value, ct);
        var verifiedSkillIds = await _assessmentRepository.GetVerifiedSkillIdsAsync(studentId.Value, ct);

        var viewModel = new StudentProfileViewModel
        {
            StudentId = student.Id,
            InstitutionalId = student.InstitutionalId,
            FirstName = student.FirstName,
            LastName = student.LastName,
            CGPA = student.CGPA,
            Department = student.Department,
            Biography = student.Biography,
            Interests = student.Interests,
            CurrentSkills = skills,
            VerifiedSkillIds = verifiedSkillIds
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(StudentProfileViewModel model, CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound("Student profile not found.");
        }

        var existing = await _studentRepository.GetByIdAsync(studentId.Value, ct);
        if (existing is null)
        {
            return NotFound("Student profile not found.");
        }

        // Server-side enforcement: InstitutionalId is strictly immutable
        if (!string.Equals(model.InstitutionalId, existing.InstitutionalId, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.InstitutionalId), "Institutional ID cannot be modified.");
        }

        if (!ModelState.IsValid)
        {
            model.InstitutionalId = existing.InstitutionalId;
            model.CurrentSkills = await _studentRepository.GetStudentSkillsAsync(studentId.Value, ct);
            model.VerifiedSkillIds = await _assessmentRepository.GetVerifiedSkillIdsAsync(studentId.Value, ct);
            return View(model);
        }

        existing.FirstName = model.FirstName.Trim();
        existing.LastName = model.LastName.Trim();
        existing.CGPA = model.CGPA;
        existing.Department = model.Department.Trim();
        existing.Biography = model.Biography?.Trim();
        existing.Interests = model.Interests?.Trim();

        await _studentRepository.UpdateProfileAsync(existing, ct);
        TempData["SuccessMessage"] = "Profile updated successfully!";

        return RedirectToAction(nameof(Index));
    }
}
