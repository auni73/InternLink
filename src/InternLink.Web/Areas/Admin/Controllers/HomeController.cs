using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Services.Dashboard;

namespace InternLink.Web.Areas.Admin.Controllers;

public class HomeController : AdminControllerBase
{
    private readonly IAdminDashboardService _dashboard;

    public HomeController(IAdminDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await _dashboard.GetAsync(ct);
        return View(model);
    }
}
