using System.ComponentModel.DataAnnotations;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;

namespace InternLink.Web.ViewModels;

public class CompanyProfileViewModel
{
    public Guid CompanyId { get; set; }

    [Required(ErrorMessage = "Company name is required.")]
    [MaxLength(200, ErrorMessage = "Company name cannot exceed 200 characters.")]
    [Display(Name = "Company Name")]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Website URL cannot exceed 500 characters.")]
    [Url(ErrorMessage = "Please enter a valid absolute URL (e.g., https://example.com).")]
    [Display(Name = "Corporate Website")]
    public string? CorporateWebsite { get; set; }

    [Required(ErrorMessage = "Industry sector is required.")]
    [MaxLength(100, ErrorMessage = "Industry sector cannot exceed 100 characters.")]
    [Display(Name = "Industry Sector")]
    public string IndustrySector { get; set; } = string.Empty;

    // INVARIANT: Read-only on Company profile; modified exclusively by Admin.
    public VerificationStatus VerificationStatus { get; set; }
}

public class CompanyJobListItemViewModel
{
    public Guid JobId { get; set; }
    public string Title { get; set; } = string.Empty;
    public LocationType LocationType { get; set; }
    public DateTimeOffset DeadLine { get; set; }
    public bool IsApproved { get; set; }
    public bool IsClosed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int ApplicantCount { get; set; }

    public string StatusBadgeText
    {
        get
        {
            if (IsClosed) return "Closed";
            if (DeadLine < DateTimeOffset.UtcNow) return "Expired";
            if (!IsApproved) return "Pending Approval";
            return "Live";
        }
    }

    public string StatusBadgeClass
    {
        get
        {
            if (IsClosed) return "bg-secondary";
            if (DeadLine < DateTimeOffset.UtcNow) return "bg-dark";
            if (!IsApproved) return "bg-warning text-dark";
            return "bg-success";
        }
    }
}

public class CompanyJobEditViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Job title is required.")]
    [MaxLength(200, ErrorMessage = "Job title cannot exceed 200 characters.")]
    [Display(Name = "Job Title")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Job description is required.")]
    [Display(Name = "Core Description")]
    public string CoreDescription { get; set; } = string.Empty;

    [Display(Name = "Selection Criteria")]
    public string? SelectionCriteria { get; set; }

    [Required(ErrorMessage = "Location type is required.")]
    [Display(Name = "Workplace Type")]
    public LocationType LocationType { get; set; } = LocationType.OnSite;

    [Required(ErrorMessage = "Application deadline is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Application Deadline")]
    public DateTime DeadLineDate { get; set; } = DateTime.UtcNow.AddDays(14).Date;

    public bool IsApproved { get; set; }
    public bool IsClosed { get; set; }

    public List<JobSkillWeightDto> SelectedSkills { get; set; } = new();
    public IReadOnlyList<Skill> AvailableSkills { get; set; } = new List<Skill>();
}

public class JobSkillWeightDto
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public byte Weight { get; set; } = 3; // 1 = Nice to have, 3 = Preferred, 5 = Critical
}
