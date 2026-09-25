using System.Security.Claims;
using InternLink.Web.Controllers;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.Notification;
using InternLink.Web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InternLink.Tests;

public class NotificationServiceTests
{
    private class FakeNotificationRepository : INotificationRepository
    {
        public List<Notification> Notifications { get; } = new();

        public Task<Guid> CreateNotificationAsync(Guid targetUserId, string textPayload, string routingUrl, CancellationToken ct = default)
        {
            var id = Guid.NewGuid();
            Notifications.Add(new Notification
            {
                Id = id,
                TargetUserId = targetUserId,
                TextPayload = textPayload,
                EventRoutingUrl = routingUrl,
                IsRead = false,
                TimeTriggered = DateTimeOffset.UtcNow
            });
            return Task.FromResult(id);
        }

        public Task<int> GetUnreadCountAsync(Guid targetUserId, CancellationToken ct = default)
        {
            var count = Notifications.Count(n => n.TargetUserId == targetUserId && !n.IsRead);
            return Task.FromResult(count);
        }

        public Task<IReadOnlyList<Notification>> GetRecentAsync(Guid targetUserId, int take = 10, CancellationToken ct = default)
        {
            var list = Notifications
                .Where(n => n.TargetUserId == targetUserId)
                .OrderByDescending(n => n.TimeTriggered)
                .Take(take)
                .ToList();
            return Task.FromResult<IReadOnlyList<Notification>>(list);
        }

        public Task<bool> MarkAsReadAsync(Guid notificationId, Guid targetUserId, CancellationToken ct = default)
        {
            var item = Notifications.FirstOrDefault(n => n.Id == notificationId && n.TargetUserId == targetUserId);
            if (item == null) return Task.FromResult(false);
            item.IsRead = true;
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task CreateAsync_ValidParameters_StoresNotificationAndReturnsId()
    {
        var repo = new FakeNotificationRepository();
        var service = new NotificationService(repo, NullLogger<NotificationService>.Instance);
        var targetUserId = Guid.NewGuid();

        var id = await service.CreateAsync(targetUserId, "Your application was screened", "/Student/Applications");

        Assert.NotEqual(Guid.Empty, id);
        Assert.Single(repo.Notifications);
        Assert.Equal("Your application was screened", repo.Notifications[0].TextPayload);
        Assert.Equal("/Student/Applications", repo.Notifications[0].EventRoutingUrl);
        Assert.False(repo.Notifications[0].IsRead);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsAccurateCount()
    {
        var repo = new FakeNotificationRepository();
        var service = new NotificationService(repo, NullLogger<NotificationService>.Instance);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await service.CreateAsync(userA, "Note 1", "/url1");
        await service.CreateAsync(userA, "Note 2", "/url2");
        await service.CreateAsync(userB, "Note for B", "/urlB");

        var countA = await service.GetUnreadCountAsync(userA);
        var countB = await service.GetUnreadCountAsync(userB);

        Assert.Equal(2, countA);
        Assert.Equal(1, countB);
    }

    [Fact]
    public async Task MarkAsRead_RightfulOwner_Succeeds()
    {
        var repo = new FakeNotificationRepository();
        var service = new NotificationService(repo, NullLogger<NotificationService>.Instance);
        var user = Guid.NewGuid();

        var noteId = await service.CreateAsync(user, "Note 1", "/url1");
        var marked = await service.MarkAsReadAsync(noteId, user);

        Assert.True(marked);
        var count = await service.GetUnreadCountAsync(user);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task MarkAsRead_WrongOwner_GuessedIdProtection_Fails()
    {
        var repo = new FakeNotificationRepository();
        var service = new NotificationService(repo, NullLogger<NotificationService>.Instance);
        var rightfulUser = Guid.NewGuid();
        var attackerUser = Guid.NewGuid();

        var noteId = await service.CreateAsync(rightfulUser, "Private Notification", "/url1");
        
        // Attacker attempts to mark rightfulUser's notification as read
        var marked = await service.MarkAsReadAsync(noteId, attackerUser);

        Assert.False(marked);
        Assert.Equal(1, await service.GetUnreadCountAsync(rightfulUser));
    }

    [Fact]
    public async Task Controller_UnreadCount_ReturnsJsonWithCount()
    {
        var repo = new FakeNotificationRepository();
        var service = new NotificationService(repo, NullLogger<NotificationService>.Instance);
        var user = Guid.NewGuid();

        await service.CreateAsync(user, "Sample Note", "/url");

        var controller = new NotificationsController(service, NullLogger<NotificationsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, user.ToString()),
                        new Claim(ClaimTypes.Role, "Student")
                    ], "TestAuth"))
                }
            }
        };

        var result = await controller.UnreadCount(CancellationToken.None);
        var jsonResult = Assert.IsType<JsonResult>(result);
        
        // Verify count property exists and equals 1
        var prop = jsonResult.Value?.GetType().GetProperty("count");
        Assert.NotNull(prop);
        Assert.Equal(1, prop.GetValue(jsonResult.Value));
    }
}
