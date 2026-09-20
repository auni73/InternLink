using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Notification;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepo;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository notificationRepo,
        ILogger<NotificationService> logger)
    {
        _notificationRepo = notificationRepo;
        _logger = logger;
    }

    public async Task<Guid> CreateAsync(
        Guid targetUserId, 
        string text, 
        string routingUrl, 
        CancellationToken ct = default)
    {
        if (targetUserId == Guid.Empty)
        {
            _logger.LogWarning("Attempted to create notification with empty TargetUserId");
            return Guid.Empty;
        }

        var safeText = text?.Length > 500 ? text[..500] : (text ?? string.Empty);
        var safeUrl = routingUrl?.Length > 500 ? routingUrl[..500] : (routingUrl ?? string.Empty);

        var id = await _notificationRepo.CreateNotificationAsync(targetUserId, safeText, safeUrl, ct);
        _logger.LogInformation("Notification {NotificationId} created for user {TargetUserId}", id, targetUserId);
        return id;
    }

    public async Task<int> GetUnreadCountAsync(Guid targetUserId, CancellationToken ct = default)
    {
        if (targetUserId == Guid.Empty) return 0;
        return await _notificationRepo.GetUnreadCountAsync(targetUserId, ct);
    }

    public async Task<IReadOnlyList<NotificationItemDto>> GetRecentAsync(
        Guid targetUserId, 
        int take = 10, 
        CancellationToken ct = default)
    {
        if (targetUserId == Guid.Empty) return [];

        var list = await _notificationRepo.GetRecentAsync(targetUserId, take, ct);
        return list.Select(n => new NotificationItemDto
        {
            Id = n.Id,
            TextPayload = n.TextPayload,
            EventRoutingUrl = n.EventRoutingUrl,
            IsRead = n.IsRead,
            TimeTriggered = n.TimeTriggered,
            TimeAgo = NotificationItemDto.FormatTimeAgo(n.TimeTriggered)
        }).ToList();
    }

    public async Task<bool> MarkAsReadAsync(
        Guid notificationId, 
        Guid currentUserId, 
        CancellationToken ct = default)
    {
        if (notificationId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return false;
        }

        var success = await _notificationRepo.MarkAsReadAsync(notificationId, currentUserId, ct);
        if (!success)
        {
            _logger.LogWarning(
                "Failed to mark notification {NotificationId} as read for user {CurrentUserId} (either not found or ownership mismatch)",
                notificationId,
                currentUserId);
        }
        return success;
    }
}
