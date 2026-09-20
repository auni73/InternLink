using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InternLink.Web.Services.Notification;

namespace InternLink.Web.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationService notificationService,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    private Guid? CurrentUserId
    {
        get
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(idClaim, out var id) ? id : null;
        }
    }

    [HttpGet]
    [Route("Notifications/UnreadCount")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null)
        {
            return Unauthorized();
        }

        var count = await _notificationService.GetUnreadCountAsync(userId.Value, ct);
        return Json(new { count });
    }

    [HttpGet]
    [Route("Notifications/Recent")]
    public async Task<IActionResult> Recent([FromQuery] int take = 10, CancellationToken ct = default)
    {
        var userId = CurrentUserId;
        if (userId is null)
        {
            return Unauthorized();
        }

        var items = await _notificationService.GetRecentAsync(userId.Value, take, ct);
        return Json(new { items });
    }

    [HttpPost]
    [Route("Notifications/{id:guid}/Read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null)
        {
            return Unauthorized();
        }

        var success = await _notificationService.MarkAsReadAsync(id, userId.Value, ct);
        if (!success)
        {
            return NotFound(new { error = "Notification not found or access denied." });
        }

        return Json(new { success = true });
    }
}
