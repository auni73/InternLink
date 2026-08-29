using System.ComponentModel.DataAnnotations;
using InternLink.Web.Models;

namespace InternLink.Web.ViewModels;

public class CounselorStudentDirectoryItemViewModel
{
    public Guid StudentId { get; set; }
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public decimal CGPA { get; set; }
    public string Department { get; set; } = string.Empty;
    public string InstitutionalId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int ResumeCount { get; set; }
    public int ApplicationCount { get; set; }
}

public class CounselorStudentDirectoryViewModel
{
    public IReadOnlyList<CounselorStudentDirectoryItemViewModel> Students { get; set; } = Array.Empty<CounselorStudentDirectoryItemViewModel>();
    public string? Search { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int TotalCount { get; set; }

    public int TotalPages => PageSize > 0 ? Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize)) : 1;
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
}

public class CounselorFeedbackItemViewModel
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid CounselorUserId { get; set; }
    public string CounselorName { get; set; } = string.Empty;
    public string CounselorEmail { get; set; } = string.Empty;
    public string NarrativeMarkdown { get; set; } = string.Empty;
    public string RenderedHtml { get; set; } = string.Empty;
    public DateTimeOffset MeetingDate { get; set; }
}

public class CounselorFeedbackCreateViewModel
{
    public Guid StudentId { get; set; }

    [Required(ErrorMessage = "Advising feedback notes are required.")]
    [StringLength(5000, ErrorMessage = "Feedback notes cannot exceed 5,000 characters.")]
    [Display(Name = "Feedback Notes (Markdown)")]
    public string NarrativeMarkdown { get; set; } = string.Empty;

    // Past or future allowed — counselors log past meetings or schedule future notes
    [Required(ErrorMessage = "Meeting date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Meeting Date")]
    public DateTimeOffset MeetingDate { get; set; } = DateTimeOffset.UtcNow;
}

public class CounselorStudentDetailViewModel
{
    public Student Student { get; set; } = null!;
    public string UserEmail { get; set; } = string.Empty;
    public IReadOnlyList<StudentSkill> Skills { get; set; } = Array.Empty<StudentSkill>();
    public IReadOnlyList<Resume> Resumes { get; set; } = Array.Empty<Resume>();
    public IReadOnlyList<StudentApplicationItemViewModel> Applications { get; set; } = Array.Empty<StudentApplicationItemViewModel>();
    public IReadOnlyList<CounselorFeedbackItemViewModel> Feedbacks { get; set; } = Array.Empty<CounselorFeedbackItemViewModel>();
    public CounselorFeedbackCreateViewModel NewFeedback { get; set; } = new();
    public string ActiveTab { get; set; } = "profile";
}

public class StudentAdvisingNotesViewModel
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public IReadOnlyList<CounselorFeedbackItemViewModel> Feedbacks { get; set; } = Array.Empty<CounselorFeedbackItemViewModel>();
}
