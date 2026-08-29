using InternLink.Web.Services.Assessment;
using InternLink.Web.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace InternLink.Tests;

public class AssessmentServiceTests
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public AssessmentServiceTests()
    {
        _dataProtectionProvider = new EphemeralDataProtectionProvider();
    }

    [Fact]
    public void QuestionProvider_GetExamQuestions_OmitsCorrectAnswers()
    {
        // Arrange
        var testEnv = new TestWebHostEnvironment(GetWebProjectRoot());
        var provider = new AssessmentQuestionProvider(testEnv, NullLogger<AssessmentQuestionProvider>.Instance);

        // Act
        var questions = provider.GetExamQuestions("C#");

        // Assert
        Assert.NotNull(questions);
        Assert.Equal(5, questions.Count);
        foreach (var q in questions)
        {
            Assert.False(string.IsNullOrWhiteSpace(q.QuestionId));
            Assert.False(string.IsNullOrWhiteSpace(q.QuestionText));
            Assert.Equal(4, q.Options.Count);
            // Property CorrectOptionIndex is not on AssessmentQuestionViewModel
        }
    }

    [Theory]
    [InlineData(5, 100, true)]
    [InlineData(4, 80, true)]
    [InlineData(3, 60, false)]
    [InlineData(2, 40, false)]
    [InlineData(0, 0, false)]
    public void QuestionProvider_Evaluate_ComputesScoreAndPassStatusAccurately(int correctAnswersCount, int expectedScore, bool expectedPass)
    {
        // Arrange
        var testEnv = new TestWebHostEnvironment(GetWebProjectRoot());
        var provider = new AssessmentQuestionProvider(testEnv, NullLogger<AssessmentQuestionProvider>.Instance);

        // Simulated submission: C# questions 1-5 have option index 0 as correct answer in seed
        var answers = new List<AssessmentAnswerDto>();
        for (int i = 1; i <= 5; i++)
        {
            answers.Add(new AssessmentAnswerDto
            {
                QuestionId = $"cs-{i}",
                SelectedOptionIndex = i <= correctAnswersCount ? 0 : 1 // 0 is correct, 1 is wrong
            });
        }

        // Act
        var result = provider.Evaluate("C#", answers);

        // Assert
        Assert.Equal(expectedScore, result.AchievedScore);
        Assert.Equal(correctAnswersCount, result.CorrectCount);
        Assert.Equal(5, result.TotalQuestions);
        Assert.Equal(expectedPass, result.IsPassed);
        Assert.Equal(5, result.QuestionFeedback.Count);
    }

    [Fact]
    public void SessionService_ValidateToken_SucceedsWithinTimeLimit()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var sessionService = new AssessmentSessionService(
            _dataProtectionProvider, 
            fakeTime, 
            NullLogger<AssessmentSessionService>.Instance);

        var studentId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var questionIds = new[] { "q1", "q2", "q3", "q4", "q5" };

        var token = sessionService.CreateSessionToken(studentId, skillId, "C#", questionIds);

        // Advance time by 5 minutes (within 10m limit)
        fakeTime.Advance(TimeSpan.FromMinutes(5));

        // Act
        var (isValid, payload, errorMessage) = sessionService.ValidateSessionToken(token, studentId, skillId);

        // Assert
        Assert.True(isValid);
        Assert.NotNull(payload);
        Assert.Null(errorMessage);
        Assert.Equal(studentId, payload.StudentId);
        Assert.Equal(skillId, payload.SkillId);
        Assert.Equal("C#", payload.SkillName);
    }

    [Fact]
    public void SessionService_ValidateToken_RejectsExpiredToken_BeyondTenMinutesAndGrace()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var sessionService = new AssessmentSessionService(
            _dataProtectionProvider, 
            fakeTime, 
            NullLogger<AssessmentSessionService>.Instance);

        var studentId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var questionIds = new[] { "q1", "q2", "q3", "q4", "q5" };

        var token = sessionService.CreateSessionToken(studentId, skillId, "C#", questionIds);

        // Advance time by 10 minutes and 16 seconds (exceeding 10m + 15s grace = 615s)
        fakeTime.Advance(TimeSpan.FromSeconds(616));

        // Act
        var (isValid, payload, errorMessage) = sessionService.ValidateSessionToken(token, studentId, skillId);

        // Assert
        Assert.False(isValid);
        Assert.Null(payload);
        Assert.NotNull(errorMessage);
        Assert.Contains("time limit exceeded", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SessionService_ValidateToken_RejectsForgedStudentOrSkill()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var sessionService = new AssessmentSessionService(
            _dataProtectionProvider, 
            fakeTime, 
            NullLogger<AssessmentSessionService>.Instance);

        var studentId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var token = sessionService.CreateSessionToken(studentId, skillId, "C#", new[] { "q1" });

        var attackerStudentId = Guid.NewGuid();
        var differentSkillId = Guid.NewGuid();

        // Act & Assert
        var (isStudentValid, _, _) = sessionService.ValidateSessionToken(token, attackerStudentId, skillId);
        Assert.False(isStudentValid);

        var (isSkillValid, _, _) = sessionService.ValidateSessionToken(token, studentId, differentSkillId);
        Assert.False(isSkillValid);
    }

    [Theory]
    [InlineData(0, "Backend")]
    [InlineData(1, "Frontend")]
    [InlineData(2, "DevOps")]
    [InlineData(3, "Soft Skills")]
    [InlineData(99, "General")]
    public void SkillAssessmentListItem_DomainName_MapsCorrectly(byte domainClassification, string expectedDomain)
    {
        // Arrange
        var item = new SkillAssessmentListItemViewModel
        {
            DomainClassification = domainClassification
        };

        // Assert
        Assert.Equal(expectedDomain, item.DomainName);
    }

    private static string GetWebProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "src", "InternLink.Web");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            current = current.Parent;
        }

        return AppContext.BaseDirectory;
    }
}

public class TestWebHostEnvironment : IWebHostEnvironment
{
    public TestWebHostEnvironment(string contentRootPath)
    {
        ContentRootPath = contentRootPath;
        WebRootPath = Path.Combine(contentRootPath, "wwwroot");
        EnvironmentName = "Development";
        ApplicationName = "InternLink.Web";
        ContentRootFileProvider = new NullFileProvider();
        WebRootFileProvider = new NullFileProvider();
    }

    public string WebRootPath { get; set; }
    public IFileProvider WebRootFileProvider { get; set; }
    public string ApplicationName { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }
    public string ContentRootPath { get; set; }
    public string EnvironmentName { get; set; }
}
