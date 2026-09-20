using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Notification;

public interface INotificationService
{
    Task<Guid> CreateAsync(Guid targetUserId, string text, string routingUrl, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid targetUserId, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationItemDto>> GetRecentAsync(Guid targetUserId, int take = 10, CancellationToken ct = default);
    Task<bool> MarkAsReadAsync(Guid notificationId, Guid currentUserId, CancellationToken ct = default);
}
