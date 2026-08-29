using System.Text.Json;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Assessment;

public class AssessmentQuestionProvider : IAssessmentQuestionProvider
{
    private readonly Dictionary<string, StoredSkillQuestions> _questionsBySkill = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<AssessmentQuestionProvider> _logger;

    public AssessmentQuestionProvider(IWebHostEnvironment env, ILogger<AssessmentQuestionProvider> logger)
    {
        _logger = logger;
        LoadQuestions(env.ContentRootPath);
    }

    private void LoadQuestions(string contentRootPath)
    {
        try
        {
            var filePath = Path.Combine(contentRootPath, "Data", "SeedData", "assessment-questions.json");
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Assessment questions file not found at: {FilePath}", filePath);
                return;
            }

            var json = File.ReadAllText(filePath);
            var items = JsonSerializer.Deserialize<List<StoredSkillQuestions>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (items != null)
            {
                foreach (var item in items)
                {
                    if (!string.IsNullOrWhiteSpace(item.SkillName))
                    {
                        _questionsBySkill[item.SkillName.Trim()] = item;
                    }
                }
                _logger.LogInformation("Loaded {SkillCount} skill question banks for assessments.", _questionsBySkill.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load assessment questions from JSON.");
        }
    }

    public bool HasQuestionsForSkill(string skillName)
    {
        return !string.IsNullOrWhiteSpace(skillName) && _questionsBySkill.ContainsKey(skillName.Trim());
    }

    public IReadOnlyList<AssessmentQuestionViewModel> GetExamQuestions(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName) || !_questionsBySkill.TryGetValue(skillName.Trim(), out var bank))
        {
            return Array.Empty<AssessmentQuestionViewModel>();
        }

        // Return questions without CorrectOptionIndex to prevent leakage in HTML/JavaScript
        return bank.Questions.Select(q => new AssessmentQuestionViewModel
        {
            QuestionId = q.QuestionId,
            QuestionText = q.QuestionText,
            Options = q.Options.ToList()
        }).ToList();
    }

    public AssessmentEvaluationResult Evaluate(string skillName, IReadOnlyList<AssessmentAnswerDto> answers)
    {
        if (string.IsNullOrWhiteSpace(skillName) || !_questionsBySkill.TryGetValue(skillName.Trim(), out var bank) || bank.Questions.Count == 0)
        {
            return new AssessmentEvaluationResult
            {
                AchievedScore = 0,
                CorrectCount = 0,
                TotalQuestions = 0,
                QuestionFeedback = Array.Empty<QuestionFeedbackItemViewModel>()
            };
        }

        var answerMap = answers?.ToDictionary(a => a.QuestionId, a => a.SelectedOptionIndex) ?? new Dictionary<string, int?>();
        var feedbackList = new List<QuestionFeedbackItemViewModel>();
        var correctCount = 0;

        foreach (var q in bank.Questions)
        {
            var userChoice = answerMap.TryGetValue(q.QuestionId, out var selected) ? selected : null;
            var isCorrect = userChoice.HasValue && userChoice.Value == q.CorrectOptionIndex;

            if (isCorrect)
            {
                correctCount++;
            }

            feedbackList.Add(new QuestionFeedbackItemViewModel
            {
                QuestionId = q.QuestionId,
                QuestionText = q.QuestionText,
                Options = q.Options.ToList(),
                SelectedOptionIndex = userChoice,
                CorrectOptionIndex = q.CorrectOptionIndex,
                IsCorrect = isCorrect,
                Explanation = q.Explanation
            });
        }

        var total = bank.Questions.Count;
        var score = total > 0 ? (int)Math.Round((double)correctCount / total * 100) : 0;

        return new AssessmentEvaluationResult
        {
            AchievedScore = score,
            CorrectCount = correctCount,
            TotalQuestions = total,
            QuestionFeedback = feedbackList
        };
    }
}
