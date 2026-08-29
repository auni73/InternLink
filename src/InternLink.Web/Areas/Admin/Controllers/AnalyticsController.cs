using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Repositories.Interface;

namespace InternLink.Web.Areas.Admin.Controllers;

public class AnalyticsController : AdminControllerBase
{
    private readonly IAnalyticsRepository _analyticsRepo;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        IAnalyticsRepository analyticsRepo,
        ILogger<AnalyticsController> logger)
    {
        _analyticsRepo = analyticsRepo;
        _logger = logger;
    }

    [HttpGet]
    [Route("Admin/Analytics")]
    [Route("Admin/Analytics/Index")]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var model = await _analyticsRepo.GetAdminAnalyticsAsync(ct);
        _logger.LogInformation("Admin {AdminId} loaded Analytics Cockpit", CurrentUserId);
        return View(model);
    }
}
