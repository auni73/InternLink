using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Company.Controllers;

public class ProfileController : CompanyControllerBase
{
    private readonly ICompanyRepository _companyRepository;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(ICompanyRepository companyRepository, ILogger<ProfileController> logger)
    {
        _companyRepository = companyRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var company = await _companyRepository.GetByUserIdAsync(CurrentUserId, ct);
        if (company is null)
        {
            return NotFound("Company profile not found.");
        }

        var viewModel = new CompanyProfileViewModel
        {
            CompanyId = company.Id,
            CompanyName = company.CompanyName,
            CorporateWebsite = company.CorporateWebsite,
            IndustrySector = company.IndustrySector,
            VerificationStatus = company.VerificationStatus
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CompanyProfileViewModel model, CancellationToken ct)
    {
        var company = await _companyRepository.GetByUserIdAsync(CurrentUserId, ct);
        if (company is null)
        {
            return NotFound("Company profile not found.");
        }

        if (!ModelState.IsValid)
        {
            model.CompanyId = company.Id;
            model.VerificationStatus = company.VerificationStatus;
            return View(model);
        }

        // Validate website absolute URL structure if provided
        if (!string.IsNullOrWhiteSpace(model.CorporateWebsite))
        {
            if (!Uri.TryCreate(model.CorporateWebsite.Trim(), UriKind.Absolute, out var uriResult) ||
                (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
            {
                ModelState.AddModelError(nameof(model.CorporateWebsite), "Corporate website must be a valid HTTP or HTTPS URL (e.g., https://example.com).");
                model.CompanyId = company.Id;
                model.VerificationStatus = company.VerificationStatus;
                return View(model);
            }
        }

        company.CompanyName = model.CompanyName.Trim();
        company.CorporateWebsite = string.IsNullOrWhiteSpace(model.CorporateWebsite) ? null : model.CorporateWebsite.Trim();
        company.IndustrySector = model.IndustrySector.Trim();

        await _companyRepository.UpdateProfileAsync(company, ct);
        TempData["SuccessMessage"] = "Company profile updated successfully!";

        return RedirectToAction(nameof(Index));
    }
}
