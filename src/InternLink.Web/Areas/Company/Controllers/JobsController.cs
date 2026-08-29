using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Filters;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Company.Controllers;

public class JobsController : CompanyControllerBase
{
    private readonly IJobRepository _jobRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly ISkillRepository _skillRepository;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        IJobRepository jobRepository,
        ICompanyRepository companyRepository,
        ISkillRepository skillRepository,
        ILogger<JobsController> logger)
    {
        _jobRepository = jobRepository;
        _companyRepository = companyRepository;
        _skillRepository = skillRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var companyId = await GetCompanyIdAsync(ct);
        if (companyId is null)
        {
            return NotFound("Company profile not found.");
        }

        var jobs = await _jobRepository.GetCompanyJobsAsync(companyId.Value, ct);
        var verificationStatus = await _companyRepository.GetVerificationStatusAsync(companyId.Value, ct);
        ViewBag.VerificationStatus = verificationStatus;

        return View(jobs);
    }

    [HttpGet]
    [EnsureVerifiedCompany]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var skills = await _skillRepository.GetAllAsync(ct);
        var viewModel = new CompanyJobEditViewModel
        {
            DeadLineDate = DateTime.UtcNow.AddDays(14).Date,
            AvailableSkills = skills
        };

        return View(viewModel);
    }

    [HttpPost]
    [EnsureVerifiedCompany]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CompanyJobEditViewModel model, CancellationToken ct)
    {
        var companyId = await GetCompanyIdAsync(ct);
        if (companyId is null)
        {
            return NotFound("Company profile not found.");
        }

        // Server-side strict future validation (at least tomorrow)
        if (model.DeadLineDate.Date <= DateTime.UtcNow.Date)
        {
            ModelState.AddModelError(nameof(model.DeadLineDate), "Application deadline must be a future date (at least tomorrow).");
        }

        if (!ModelState.IsValid)
        {
            model.AvailableSkills = await _skillRepository.GetAllAsync(ct);
            return View(model);
        }

        var jobId = await _jobRepository.CreateJobWithSkillsAsync(companyId.Value, model, ct);
        _logger.LogInformation("Company {CompanyId} created job posting {JobId}.", companyId.Value, jobId);

        TempData["SuccessMessage"] = "Job posting created successfully! It is currently pending administrator verification before appearing in student search.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [EnsureVerifiedCompany]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var companyId = await GetCompanyIdAsync(ct);
        if (companyId is null)
        {
            return NotFound("Company profile not found.");
        }

        var viewModel = await _jobRepository.GetCompanyJobForEditAsync(id, companyId.Value, ct);
        if (viewModel is null)
        {
            return NotFound("Job posting not found or you do not have permission to access it.");
        }

        viewModel.AvailableSkills = await _skillRepository.GetAllAsync(ct);
        return View(viewModel);
    }

    [HttpPost]
    [EnsureVerifiedCompany]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CompanyJobEditViewModel model, CancellationToken ct)
    {
        var companyId = await GetCompanyIdAsync(ct);
        if (companyId is null)
        {
            return NotFound("Company profile not found.");
        }

        // Server-side strict future validation (at least tomorrow)
        if (model.DeadLineDate.Date <= DateTime.UtcNow.Date)
        {
            ModelState.AddModelError(nameof(model.DeadLineDate), "Application deadline must be a future date (at least tomorrow).");
        }

        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.AvailableSkills = await _skillRepository.GetAllAsync(ct);
            return View(model);
        }

        var updated = await _jobRepository.UpdateJobWithSkillsAsync(id, companyId.Value, model, ct);
        if (!updated)
        {
            return NotFound("Job posting not found or you do not have permission to access it.");
        }

        _logger.LogInformation("Company {CompanyId} updated job posting {JobId}.", companyId.Value, id);
        TempData["SuccessMessage"] = "Job posting updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [EnsureVerifiedCompany]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        var companyId = await GetCompanyIdAsync(ct);
        if (companyId is null)
        {
            return NotFound("Company profile not found.");
        }

        var closed = await _jobRepository.CloseJobAsync(id, companyId.Value, ct);
        if (!closed)
        {
            return NotFound("Job posting not found or you do not have permission to access it.");
        }

        _logger.LogInformation("Company {CompanyId} closed job posting {JobId}.", companyId.Value, id);

        var isJsonRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                            Request.Headers.Accept.ToString().Contains("application/json") ||
                            (Request.ContentType?.Contains("application/json") ?? false);

        if (isJsonRequest)
        {
            return Json(new { success = true, message = "Job posting closed successfully. It is now hidden from student searches." });
        }

        TempData["SuccessMessage"] = "Job posting closed successfully. It is now hidden from student searches.";
        return RedirectToAction(nameof(Index));
    }
}
