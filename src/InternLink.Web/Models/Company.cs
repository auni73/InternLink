using InternLink.Web.Models.Enums;

namespace InternLink.Web.Models;

public class Company
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CorporateWebsite { get; set; }
    public string IndustrySector { get; set; } = string.Empty;

    // INVARIANT: VerificationStatus is modified exclusively by Administrators.
    // Companies cannot mutate their own verification state during profile updates.
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;
    public string? AdminRejectionReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual AppUser User { get; set; } = null!;
    public virtual ICollection<Job> Jobs { get; set; } = new List<Job>();
}
