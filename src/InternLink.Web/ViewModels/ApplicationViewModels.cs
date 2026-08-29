using InternLink.Web.Models.Enums;

namespace InternLink.Web.ViewModels;

public class StudentApplicationItemViewModel
{
    public Guid ApplicationId { get; set; }
    public Guid JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public LocationType LocationType { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public ApplicationStatus Status { get; set; }
    public Guid? AttachedResumeId { get; set; }
    public string? AttachedResumeName { get; set; }
    public string? CoverLetterText { get; set; }

    // Scheduled Interview details if present
    public Guid? InterviewId { get; set; }
    public DateTimeOffset? InterviewDateTime { get; set; }
    public string? MeetingLink { get; set; }
    public InterviewStatus? InterviewStatus { get; set; }
}

public class StudentApplicationsViewModel
{
    public IReadOnlyList<StudentApplicationItemViewModel> Applications { get; set; } = new List<StudentApplicationItemViewModel>();
}
