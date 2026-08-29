using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Services.Dashboard;

namespace InternLink.Web.Areas.Counselor.Controllers;

public class HomeController : CounselorControllerBase
{
    private readonly ICounselorDashboardService _dashboard;

    public HomeController(ICounselorDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await _dashboard.GetAsync(ct);
        return View(model);
    }
}
