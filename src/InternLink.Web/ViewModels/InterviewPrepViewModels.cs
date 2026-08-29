using System.Text.Json.Serialization;
using InternLink.Web.Models.Enums;

namespace InternLink.Web.ViewModels;

public sealed class InterviewQuestion
{
    [JsonPropertyName("questionText")]
    public string QuestionText { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = InterviewQuestionCategories.Technical;
}

public sealed class QuestionBankResult
{
    [JsonPropertyName("questions")]
    public List<InterviewQuestion> Questions { get; set; } = [];
}

public static class InterviewQuestionCategories
{
    public const string Technical = "Technical";
    public const string Hr = "HR";
    public const string Situational = "Situational";

    public static readonly IReadOnlyList<string> All = [Technical, Hr, Situational];

    /// <summary>Models drift on casing and wording ("hr", "Behavioural"), so unknown labels land in Situational.</summary>
    public static string Normalize(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Equals(Technical, StringComparison.OrdinalIgnoreCase))
        {
            return Technical;
        }

        if (trimmed.Equals(Hr, StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("human", StringComparison.OrdinalIgnoreCase))
        {
            return Hr;
        }

        return Situational;
    }
}

/// <summary>One line of the stored conversation. Persisted as the session's TranscriptJson array.</summary>
public sealed class TranscriptTurn
{
    [JsonPropertyName("speaker")]
    public string Speaker { get; set; } = TranscriptSpeakers.Interviewer;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("at")]
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public static class TranscriptSpeakers
{
    public const string Interviewer = "interviewer";
    public const string Student = "student";
}

public sealed class MockInterviewReport
{
    [JsonPropertyName("accuracySummary")]
    public string AccuracySummary { get; set; } = string.Empty;

    [JsonPropertyName("logicGaps")]
    public List<string> LogicGaps { get; set; } = [];

    [JsonPropertyName("improvementSuggestions")]
    public List<string> ImprovementSuggestions { get; set; } = [];

    public static MockInterviewReport Unavailable() => new()
    {
        AccuracySummary = "The coaching report could not be generated this time. Your full transcript is saved below."
    };
}

public sealed class QuestionBankPageViewModel
{
    public IReadOnlyList<TargetJobOption> TargetJobs { get; set; } = [];
    public string DefaultRole { get; set; } = string.Empty;
}

public sealed class MockInterviewLaunchViewModel
{
    public IReadOnlyList<TargetJobOption> TargetJobs { get; set; } = [];
    public string DefaultRole { get; set; } = string.Empty;
    public IReadOnlyList<MockInterviewSessionListItem> RecentSessions { get; set; } = [];
}

public sealed class MockInterviewSessionListItem
{
    public Guid Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public MockInterviewStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int TurnCount { get; set; }
}

public sealed class MockInterviewSessionViewModel
{
    public Guid SessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public MockInterviewStatus Status { get; set; }
    public IReadOnlyList<TranscriptTurn> Transcript { get; set; } = [];
}

public sealed class MockInterviewReportViewModel
{
    public Guid SessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public MockInterviewReport Report { get; set; } = new();
    public IReadOnlyList<TranscriptTurn> Transcript { get; set; } = [];
}
