namespace InternLink.Web.ViewModels;

public class NotificationItemDto
{
    public Guid Id { get; set; }
    public string TextPayload { get; set; } = string.Empty;
    public string EventRoutingUrl { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTimeOffset TimeTriggered { get; set; }
    public string TimeAgo { get; set; } = string.Empty;

    public static string FormatTimeAgo(DateTimeOffset timeTriggered)
    {
        var span = DateTimeOffset.UtcNow - timeTriggered;
        if (span.TotalSeconds < 60)
        {
            return "Just now";
        }
        if (span.TotalMinutes < 60)
        {
            var m = (int)span.TotalMinutes;
            return $"{m}m ago";
        }
        if (span.TotalHours < 24)
        {
            var h = (int)span.TotalHours;
            return $"{h}h ago";
        }
        if (span.TotalDays < 7)
        {
            var d = (int)span.TotalDays;
            return $"{d}d ago";
        }
        return timeTriggered.ToString("MMM dd");
    }
}

public class UnreadCountDto
{
    public int Count { get; set; }
}
