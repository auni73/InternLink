using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Student.Controllers;

public class ApplicationsController : StudentControllerBase
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly ILogger<ApplicationsController> _logger;

    public ApplicationsController(
        IApplicationRepository applicationRepository, 
        ILogger<ApplicationsController> logger)
    {
        _applicationRepository = applicationRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var studentId = await GetStudentIdAsync(ct);
        if (studentId is null)
        {
            return NotFound();
        }

        var applications = await _applicationRepository.GetStudentApplicationsWithDetailsAsync(studentId.Value, ct);
        var viewModel = new StudentApplicationsViewModel
        {
            Applications = applications
        };

        return View(viewModel);
    }
}
