using System.ComponentModel.DataAnnotations;

namespace InternLink.Web.ViewModels;

public class SkillAssessmentListItemViewModel
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public byte DomainClassification { get; set; }
    public string DomainName => DomainClassification switch
    {
        0 => "Backend",
        1 => "Frontend",
        2 => "DevOps",
        3 => "Soft Skills",
        _ => "General"
    };

    public bool IsVerified { get; set; }
    public int? BestScore { get; set; }
    public int AttemptsCount { get; set; }
    public DateTimeOffset? LastAttemptDate { get; set; }
}

public class StudentAssessmentsViewModel
{
    public IReadOnlyList<SkillAssessmentListItemViewModel> Skills { get; set; } = Array.Empty<SkillAssessmentListItemViewModel>();
    public int TotalVerifiedCount => Skills.Count(s => s.IsVerified);
    public int TotalSkillsCount => Skills.Count;
}

public class AssessmentExamViewModel
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public string SessionToken { get; set; } = string.Empty;
    public int DurationMinutes { get; set; } = 10;
    public IReadOnlyList<AssessmentQuestionViewModel> Questions { get; set; } = Array.Empty<AssessmentQuestionViewModel>();
}

public class AssessmentQuestionViewModel
{
    public string QuestionId { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public IReadOnlyList<string> Options { get; set; } = Array.Empty<string>();
    // NOTE: CorrectOptionIndex is intentionally absent to prevent client-side inspection
}

public class AssessmentSubmissionRequestDto
{
    [Required]
    public Guid SkillId { get; set; }

    [Required]
    public string SessionToken { get; set; } = string.Empty;

    public List<AssessmentAnswerDto> Answers { get; set; } = new();
}

public class AssessmentAnswerDto
{
    public string QuestionId { get; set; } = string.Empty;
    public int? SelectedOptionIndex { get; set; }
}

public class AssessmentResultViewModel
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public int AchievedScore { get; set; }
    public int CorrectCount { get; set; }
    public int TotalQuestions { get; set; }
    public bool IsPassed { get; set; }
    public bool WasAlreadyVerified { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public IReadOnlyList<QuestionFeedbackItemViewModel> QuestionFeedback { get; set; } = Array.Empty<QuestionFeedbackItemViewModel>();
}

public class QuestionFeedbackItemViewModel
{
    public string QuestionId { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public IReadOnlyList<string> Options { get; set; } = Array.Empty<string>();
    public int? SelectedOptionIndex { get; set; }
    public int CorrectOptionIndex { get; set; }
    public bool IsCorrect { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

public class AssessmentSessionPayload
{
    public Guid StudentId { get; set; }
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public List<string> QuestionIds { get; set; } = new();
    public DateTimeOffset IssuedAtUtc { get; set; }
}
