using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Services.Dashboard;

namespace InternLink.Web.Areas.Company.Controllers;

public class HomeController : CompanyControllerBase
{
    private readonly ICompanyDashboardService _dashboard;

    public HomeController(ICompanyDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var companyId = await GetCompanyIdAsync(ct);
        var model = await _dashboard.GetAsync(companyId, ct);
        return View(model);
    }
}
