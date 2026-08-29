using Microsoft.AspNetCore.Identity;

namespace InternLink.Web.Models;

public class AppUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual Student? StudentProfile { get; set; }
    public virtual Company? CompanyProfile { get; set; }
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public virtual ICollection<AIHistory> AIHistories { get; set; } = new List<AIHistory>();
    public virtual ICollection<OtpCode> OtpCodes { get; set; } = new List<OtpCode>();
    public virtual ICollection<CounselorFeedback> GivenCounselorFeedbacks { get; set; } = new List<CounselorFeedback>();
}
