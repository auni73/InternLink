using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.Vectors;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Areas.Admin.Controllers;

public class UsersController : AdminControllerBase
{
    private readonly IAdminModerationRepository _moderationRepo;
    private readonly IJobRepository _jobRepository;
    private readonly IJobIndexQueue _indexQueue;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IAdminModerationRepository moderationRepo,
        IJobRepository jobRepository,
        IJobIndexQueue indexQueue,
        UserManager<AppUser> userManager,
        ILogger<UsersController> logger)
    {
        _moderationRepo = moderationRepo;
        _jobRepository = jobRepository;
        _indexQueue = indexQueue;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    [Route("Admin/Users")]
    [Route("Admin/Users/Index")]
    public async Task<IActionResult> Index(
        string? role, 
        string? search, 
        int page = 1, 
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        const int pageSize = 15;

        var (users, totalCount, studentCount, companyCount, totalAll) = 
            await _moderationRepo.GetUsersAsync(role, search, page, pageSize, ct);

        var viewModel = new AdminUserListViewModel
        {
            RoleFilter = role,
            SearchQuery = search,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalStudentsCount = studentCount,
            TotalCompaniesCount = companyCount,
            TotalAllCount = totalAll,
            Users = users
        };

        return View(viewModel);
    }

    [HttpPost]
    [Route("Admin/Users/{id:guid}/Suspend")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound("User not found.");
        }

        // 1. Set IsActive = 0 in database
        await _moderationRepo.SetUserActiveStatusAsync(id, false, ct);

        // 2. Update Security Stamp to invalidate live cookie sessions
        await _userManager.UpdateSecurityStampAsync(user);

        // 3. Structured logging
        _logger.LogInformation("Admin {AdminId} Suspend User {TargetId}", CurrentUserId, id);

        // 4. A suspended company's postings must not stay discoverable via semantic search.
        foreach (var jobId in await _jobRepository.GetAllJobIdsByCompanyUserIdAsync(id, ct))
        {
            _indexQueue.TryEnqueue(new JobIndexCommand(jobId, JobIndexOperation.Delete));
        }

        var isJsonRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                            Request.Headers.Accept.ToString().Contains("application/json") ||
                            (Request.ContentType?.Contains("application/json") ?? false);

        if (isJsonRequest)
        {
            return Json(new { success = true, message = $"Account '{user.Email}' has been suspended." });
        }

        TempData["SuccessMessage"] = $"Account '{user.Email}' has been suspended.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Route("Admin/Users/{id:guid}/Reactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound("User not found.");
        }

        // 1. Set IsActive = 1 in database
        await _moderationRepo.SetUserActiveStatusAsync(id, true, ct);

        // 2. Update Security Stamp
        await _userManager.UpdateSecurityStampAsync(user);

        // 3. Structured logging
        _logger.LogInformation("Admin {AdminId} Reactivate User {TargetId}", CurrentUserId, id);

        // 4. Restore the points dropped at suspension; without this their jobs stay invisible until ReindexAll.
        foreach (var jobId in await _jobRepository.GetIndexableJobIdsByCompanyUserIdAsync(id, ct))
        {
            _indexQueue.TryEnqueue(new JobIndexCommand(jobId, JobIndexOperation.Upsert));
        }

        var isJsonRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                            Request.Headers.Accept.ToString().Contains("application/json") ||
                            (Request.ContentType?.Contains("application/json") ?? false);

        if (isJsonRequest)
        {
            return Json(new { success = true, message = $"Account '{user.Email}' has been reactivated." });
        }

        TempData["SuccessMessage"] = $"Account '{user.Email}' has been reactivated.";
        return RedirectToAction(nameof(Index));
    }
}
