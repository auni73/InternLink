using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Assessment;

public interface IAssessmentQuestionProvider
{
    bool HasQuestionsForSkill(string skillName);
    IReadOnlyList<AssessmentQuestionViewModel> GetExamQuestions(string skillName);
    AssessmentEvaluationResult Evaluate(string skillName, IReadOnlyList<AssessmentAnswerDto> answers);
}

public class AssessmentEvaluationResult
{
    public int AchievedScore { get; set; }
    public int CorrectCount { get; set; }
    public int TotalQuestions { get; set; }
    public bool IsPassed => AchievedScore >= 70;
    public IReadOnlyList<QuestionFeedbackItemViewModel> QuestionFeedback { get; set; } = Array.Empty<QuestionFeedbackItemViewModel>();
}

public class StoredSkillQuestions
{
    public string SkillName { get; set; } = string.Empty;
    public List<StoredQuestion> Questions { get; set; } = new();
}

public class StoredQuestion
{
    public string QuestionId { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public int CorrectOptionIndex { get; set; }
    public string Explanation { get; set; } = string.Empty;
}
