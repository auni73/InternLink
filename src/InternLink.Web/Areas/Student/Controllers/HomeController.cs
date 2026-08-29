using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Services.Dashboard;

namespace InternLink.Web.Areas.Student.Controllers;

public class HomeController : StudentControllerBase
{
    private readonly IStudentDashboardService _dashboard;

    public HomeController(IStudentDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        var model = await _dashboard.GetAsync(studentId, ct);
        return View(model);
    }
}
