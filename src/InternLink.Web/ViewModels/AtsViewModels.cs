using InternLink.Web.Models.Enums;

namespace InternLink.Web.ViewModels;

public class CompanyAtsBoardViewModel
{
    public Guid? SelectedJobId { get; set; }
    public IReadOnlyList<JobFilterOptionDto> CompanyJobs { get; set; } = Array.Empty<JobFilterOptionDto>();
    public IReadOnlyList<CompanyAtsApplicantItemViewModel> Applications { get; set; } = Array.Empty<CompanyAtsApplicantItemViewModel>();

    public IReadOnlyList<CompanyAtsApplicantItemViewModel> AppliedCards =>
        Applications.Where(a => a.Status == ApplicationStatus.Applied).ToList();

    public IReadOnlyList<CompanyAtsApplicantItemViewModel> ScreenedCards =>
        Applications.Where(a => a.Status == ApplicationStatus.Screened).ToList();

    public IReadOnlyList<CompanyAtsApplicantItemViewModel> ScheduledCards =>
        Applications.Where(a => a.Status == ApplicationStatus.Scheduled).ToList();

    public IReadOnlyList<CompanyAtsApplicantItemViewModel> OfferedCards =>
        Applications.Where(a => a.Status == ApplicationStatus.Offered).ToList();

    public IReadOnlyList<CompanyAtsApplicantItemViewModel> RejectedCards =>
        Applications.Where(a => a.Status == ApplicationStatus.Rejected).ToList();

    public int AppliedCount => AppliedCards.Count;
    public int ScreenedCount => ScreenedCards.Count;
    public int ScheduledCount => ScheduledCards.Count;
    public int OfferedCount => OfferedCards.Count;
    public int RejectedCount => RejectedCards.Count;
    public int TotalApplicants => Applications.Count;
}

public class CompanyAtsApplicantItemViewModel
{
    public Guid ApplicationId { get; set; }
    public Guid JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal CGPA { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public ApplicationStatus Status { get; set; }
    public Guid? AttachedResumeId { get; set; }
    public int VerifiedSkillCount { get; set; }
    public Guid? InterviewId { get; set; }
    public DateTimeOffset? InterviewDateTime { get; set; }
    public string? MeetingLink { get; set; }
    public InterviewStatus? InterviewStatus { get; set; }
}

public class CompanyAtsApplicantDetailViewModel
{
    public Guid ApplicationId { get; set; }
    public Guid JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal CGPA { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public ApplicationStatus Status { get; set; }
    public string? CoverLetterText { get; set; }
    public Guid? AttachedResumeId { get; set; }
    public IReadOnlyList<SkillAssessmentListItemViewModel> VerifiedSkills { get; set; } = Array.Empty<SkillAssessmentListItemViewModel>();
    public Guid? InterviewId { get; set; }
    public DateTimeOffset? InterviewDateTime { get; set; }
    public string? MeetingLink { get; set; }
    public InterviewStatus? InterviewStatus { get; set; }
}

public class JobFilterOptionDto
{
    public Guid JobId { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class AdvanceStatusRequestDto
{
    public string NewStatus { get; set; } = string.Empty;
    public DateTimeOffset? ScheduledDateTime { get; set; }
    public string? ContextMeetingLink { get; set; }
}

public class AdvanceStatusResponseDto
{
    public bool Success { get; set; }
    public Guid ApplicationId { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public string StatusBadgeText { get; set; } = string.Empty;
    public string? InterviewDateTime { get; set; }
    public string? MeetingLink { get; set; }
    public string? Message { get; set; }
}
