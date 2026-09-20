using InternLink.Web.Models;

namespace InternLink.Web.Repositories.Interface;

public interface INotificationRepository
{
    Task<Guid> CreateNotificationAsync(Guid targetUserId, string textPayload, string routingUrl, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid targetUserId, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetRecentAsync(Guid targetUserId, int take = 10, CancellationToken ct = default);
    Task<bool> MarkAsReadAsync(Guid notificationId, Guid targetUserId, CancellationToken ct = default);
}
