using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;

namespace InternLink.Web.Repositories.Implementation;

public class NotificationRepository : INotificationRepository
{
    private readonly ApplicationDbContext _db;

    public NotificationRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> CreateNotificationAsync(
        Guid targetUserId, 
        string textPayload, 
        string routingUrl, 
        CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var paramId = new SqlParameter("@id", id);
        var paramTargetUserId = new SqlParameter("@targetUserId", targetUserId);
        var paramText = new SqlParameter("@text", textPayload ?? string.Empty);
        var paramUrl = new SqlParameter("@url", routingUrl ?? string.Empty);

        await _db.Database.ExecuteSqlRawAsync(
            @"INSERT INTO dbo.Notifications (Id, TargetUserId, TextPayload, EventRoutingUrl, IsRead, TimeTriggered)
              VALUES (@id, @targetUserId, @text, @url, 0, SYSDATETIMEOFFSET())",
            new object[] { paramId, paramTargetUserId, paramText, paramUrl },
            ct);

        return id;
    }

    public async Task<int> GetUnreadCountAsync(Guid targetUserId, CancellationToken ct = default)
    {
        var count = await _db.Database
            .SqlQueryRaw<int>(
                @"SELECT COUNT(*) AS [Value]
                  FROM dbo.Notifications
                  WHERE TargetUserId = @targetUserId AND IsRead = 0",
                new SqlParameter("@targetUserId", targetUserId))
            .SingleOrDefaultAsync(ct);

        return count;
    }

    public async Task<IReadOnlyList<Notification>> GetRecentAsync(
        Guid targetUserId, 
        int take = 10, 
        CancellationToken ct = default)
    {
        if (take <= 0) take = 10;
        if (take > 50) take = 50;

        var notifications = await _db.Notifications
            .FromSqlRaw(
                @"SELECT TOP (@take) n.Id, n.TargetUserId, n.TextPayload, n.EventRoutingUrl, n.IsRead, n.TimeTriggered
                  FROM dbo.Notifications n
                  WHERE n.TargetUserId = @targetUserId
                  ORDER BY n.TimeTriggered DESC",
                new SqlParameter("@take", take),
                new SqlParameter("@targetUserId", targetUserId))
            .AsNoTracking()
            .ToListAsync(ct);

        return notifications;
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid targetUserId, CancellationToken ct = default)
    {
        var rowsAffected = await _db.Database.ExecuteSqlRawAsync(
            @"UPDATE dbo.Notifications
              SET IsRead = 1
              WHERE Id = @id AND TargetUserId = @targetUserId",
            new object[]
            {
                new SqlParameter("@id", notificationId),
                new SqlParameter("@targetUserId", targetUserId)
            },
            ct);

        return rowsAffected > 0;
    }
}
