using System.Text;
using System.Text.Json;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.AI;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.InterviewPrep;

public class InterviewPrepService : IInterviewPrepService
{
    private const string StrictJsonReminder = " Respond with ONLY the JSON object, with no prose and no markdown fences.";

    // Gemini expects contents to open with a user turn. The interviewer speaks first, so a fixed
    // kickoff turn leads every request. It is never stored and never shown.
    private const string KickoffPrompt = "Begin the interview. Ask your first question.";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMockInterviewRepository _sessions;
    private readonly IStudentRepository _students;
    private readonly IJobRepository _jobs;
    private readonly IGeminiClient _gemini;
    private readonly ILogger<InterviewPrepService> _logger;

    public InterviewPrepService(
        IMockInterviewRepository sessions,
        IStudentRepository students,
        IJobRepository jobs,
        IGeminiClient gemini,
        ILogger<InterviewPrepService> logger)
    {
        _sessions = sessions;
        _students = students;
        _jobs = jobs;
        _gemini = gemini;
        _logger = logger;
    }

    public async Task<IReadOnlyList<InterviewQuestion>> GenerateQuestionsAsync(
        Guid studentId,
        string role,
        Guid? jobId,
        CancellationToken ct = default)
    {
        const string systemPrompt =
            "You prepare interview question banks for university students seeking internships. " +
            "Reply with JSON only: {\"questions\":[{\"questionText\":\"...\",\"category\":\"Technical\"|\"HR\"|\"Situational\"}]}. " +
            "Return exactly 8 questions with a mix of all three categories. " +
            "Each question must be one an interviewer would actually ask for this specific role, not generic filler.";

        var userPrompt = new StringBuilder()
            .AppendLine($"Role: {role}")
            .ToString();

        if (jobId.HasValue && jobId.Value != Guid.Empty)
        {
            var job = await _jobs.GetByIdAsync(jobId.Value, ct);
            if (job is not null)
            {
                userPrompt +=
                    $"Job title: {job.Title}\nDescription: {job.CoreDescription}\nSelection criteria: {job.SelectionCriteria}\n" +
                    "Target the questions at this posting's requirements.";
            }
        }

        var userId = await ResolveUserIdAsync(studentId, ct);
        var result = await GenerateJsonAsync<QuestionBankResult>(
            systemPrompt,
            userPrompt,
            IntegrationFeature.QuestionBank,
            userId,
            ct);

        return result?.Questions?
            .Where(q => !string.IsNullOrWhiteSpace(q.QuestionText))
            .Select(q => new InterviewQuestion
            {
                QuestionText = q.QuestionText.Trim(),
                Category = InterviewQuestionCategories.Normalize(q.Category)
            })
            .ToList() ?? [];
    }

    public async Task<StartSessionResult?> StartSessionAsync(
        Guid studentId,
        string role,
        Guid? jobId,
        CancellationToken ct = default)
    {
        var userId = await ResolveUserIdAsync(studentId, ct);
        var systemPrompt = await BuildInterviewerPromptAsync(role, jobId, ct);

        GeminiResponse response;
        try
        {
            response = await _gemini.GenerateChatAsync(
                systemPrompt,
                [new ChatMessage(IsUser: true, KickoffPrompt)],
                IntegrationFeature.MockInterview,
                userId,
                ct);
        }
        catch (AiServiceException ex)
        {
            _logger.LogWarning(ex, "Could not open a mock interview for student {StudentId}.", studentId);
            return null;
        }

        var firstQuestion = response.Content.Trim();
        var transcript = new List<TranscriptTurn>
        {
            new() { Speaker = TranscriptSpeakers.Interviewer, Text = firstQuestion }
        };

        var session = new MockInterviewSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            Role = role,
            JobId = jobId == Guid.Empty ? null : jobId,
            TranscriptJson = JsonSerializer.Serialize(transcript),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _sessions.CreateSessionAsync(session, ct);
        return new StartSessionResult(session.Id, firstQuestion);
    }

    public async Task<SendMessageResult> SendMessageAsync(
        Guid sessionId,
        Guid studentId,
        string studentReply,
        CancellationToken ct = default)
    {
        var session = await _sessions.GetSessionAsync(sessionId, studentId, ct);
        if (session is null)
        {
            return new SendMessageResult(SendMessageOutcome.SessionNotFound);
        }

        if (session.Status == MockInterviewStatus.Completed)
        {
            return new SendMessageResult(SendMessageOutcome.SessionAlreadyCompleted);
        }

        var transcript = DeserializeTranscript(session.TranscriptJson);
        transcript.Add(new TranscriptTurn { Speaker = TranscriptSpeakers.Student, Text = studentReply });

        var systemPrompt = await BuildInterviewerPromptAsync(session.Role, session.JobId, ct);
        var userId = await ResolveUserIdAsync(studentId, ct);

        GeminiResponse response;
        try
        {
            response = await _gemini.GenerateChatAsync(
                systemPrompt,
                BuildChatHistory(transcript),
                IntegrationFeature.MockInterview,
                userId,
                ct);
        }
        catch (AiServiceException ex)
        {
            _logger.LogWarning(ex, "Mock interview {SessionId} could not continue.", sessionId);

            // The student's answer is persisted anyway, so a failed reply does not cost them their turn.
            await _sessions.UpdateTranscriptAsync(sessionId, studentId, JsonSerializer.Serialize(transcript), ct);
            return new SendMessageResult(SendMessageOutcome.AiUnavailable);
        }

        var aiReply = response.Content.Trim();
        transcript.Add(new TranscriptTurn { Speaker = TranscriptSpeakers.Interviewer, Text = aiReply });

        // Saved every turn: a closed tab must never lose an in-progress interview.
        await _sessions.UpdateTranscriptAsync(sessionId, studentId, JsonSerializer.Serialize(transcript), ct);
        return new SendMessageResult(SendMessageOutcome.Ok, aiReply);
    }

    public async Task<MockInterviewReport?> EndSessionAsync(
        Guid sessionId,
        Guid studentId,
        CancellationToken ct = default)
    {
        var session = await _sessions.GetSessionAsync(sessionId, studentId, ct);
        if (session is null)
        {
            return null;
        }

        if (session.Status == MockInterviewStatus.Completed)
        {
            return DeserializeReport(session.ReportJson);
        }

        var transcript = DeserializeTranscript(session.TranscriptJson);

        const string systemPrompt =
            "You are an interview coach reviewing a completed mock interview transcript. " +
            "Reply with JSON only: {\"accuracySummary\":\"...\",\"logicGaps\":[\"...\"],\"improvementSuggestions\":[\"...\"]}. " +
            "accuracySummary is two to four sentences on how well the candidate actually answered, " +
            "quoting or naming specifics from the transcript. " +
            "logicGaps lists reasoning that did not hold up, each tied to the answer it came from. " +
            "improvementSuggestions lists concrete, actionable changes. " +
            "Never invent answers the candidate did not give. Address the candidate as 'you'.";

        var userPrompt = new StringBuilder()
            .AppendLine($"Role interviewed for: {session.Role}")
            .AppendLine()
            .AppendLine("Transcript:")
            .AppendLine(RenderTranscript(transcript))
            .ToString();

        var userId = await ResolveUserIdAsync(studentId, ct);
        var report = await GenerateJsonAsync<MockInterviewReport>(
            systemPrompt,
            userPrompt,
            IntegrationFeature.MockInterview,
            userId,
            ct) ?? MockInterviewReport.Unavailable();

        await _sessions.CompleteSessionAsync(sessionId, studentId, JsonSerializer.Serialize(report), ct);
        return report;
    }

    public async Task<MockInterviewSessionViewModel?> GetSessionAsync(
        Guid sessionId,
        Guid studentId,
        CancellationToken ct = default)
    {
        var session = await _sessions.GetSessionAsync(sessionId, studentId, ct);
        if (session is null)
        {
            return null;
        }

        return new MockInterviewSessionViewModel
        {
            SessionId = session.Id,
            Role = session.Role,
            Status = session.Status,
            Transcript = DeserializeTranscript(session.TranscriptJson)
        };
    }

    public async Task<MockInterviewReportViewModel?> GetReportAsync(
        Guid sessionId,
        Guid studentId,
        CancellationToken ct = default)
    {
        var session = await _sessions.GetSessionAsync(sessionId, studentId, ct);
        if (session is null)
        {
            return null;
        }

        return new MockInterviewReportViewModel
        {
            SessionId = session.Id,
            Role = session.Role,
            CreatedAt = session.CreatedAt,
            CompletedAt = session.CompletedAt,
            Report = DeserializeReport(session.ReportJson) ?? MockInterviewReport.Unavailable(),
            Transcript = DeserializeTranscript(session.TranscriptJson)
        };
    }

    public async Task<IReadOnlyList<MockInterviewSessionListItem>> GetRecentSessionsAsync(
        Guid studentId,
        int take,
        CancellationToken ct = default)
    {
        var sessions = await _sessions.GetStudentSessionsAsync(studentId, take, ct);

        return sessions.Select(s => new MockInterviewSessionListItem
        {
            Id = s.Id,
            Role = s.Role,
            Status = s.Status,
            CreatedAt = s.CreatedAt,
            TurnCount = DeserializeTranscript(s.TranscriptJson).Count
        }).ToList();
    }

    private async Task<string> BuildInterviewerPromptAsync(string role, Guid? jobId, CancellationToken ct)
    {
        var prompt = new StringBuilder()
            .Append($"You are a technical recruiter conducting a live interview for a {role} internship. ")
            .Append("Ask exactly ONE question per turn, then stop and wait for the answer. ")
            .Append("Never ask two questions in the same message and never answer your own question. ")
            .Append("Build follow-ups on what the candidate has already said, referring back to their earlier answers. ")
            .Append("Keep each message under 60 words. Speak plainly, with no markdown, no lists, and no speaker labels. ")
            .Append("Stay in character as the interviewer at all times: never coach, never grade, ")
            .Append("and never summarise the interview, even if asked.");

        if (jobId.HasValue && jobId.Value != Guid.Empty)
        {
            var job = await _jobs.GetByIdAsync(jobId.Value, ct);
            if (job is not null)
            {
                prompt.Append($" You are hiring for this posting - {job.Title}: {job.CoreDescription} ");
                prompt.Append($"Selection criteria: {job.SelectionCriteria} ");
                prompt.Append("Draw your questions from these requirements.");
            }
        }

        return prompt.ToString();
    }

    private static List<ChatMessage> BuildChatHistory(IReadOnlyList<TranscriptTurn> transcript)
    {
        var history = new List<ChatMessage> { new(IsUser: true, KickoffPrompt) };

        history.AddRange(transcript.Select(turn =>
            new ChatMessage(turn.Speaker == TranscriptSpeakers.Student, turn.Text)));

        return history;
    }

    private static string RenderTranscript(IReadOnlyList<TranscriptTurn> transcript)
    {
        var builder = new StringBuilder();

        foreach (var turn in transcript)
        {
            var label = turn.Speaker == TranscriptSpeakers.Student ? "Candidate" : "Interviewer";
            builder.AppendLine($"{label}: {turn.Text}");
        }

        return builder.ToString();
    }

    private List<TranscriptTurn> DeserializeTranscript(string? transcriptJson)
    {
        if (string.IsNullOrWhiteSpace(transcriptJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<TranscriptTurn>>(transcriptJson, SerializerOptions) ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Stored mock interview transcript could not be read.");
            return [];
        }
    }

    private MockInterviewReport? DeserializeReport(string? reportJson)
    {
        if (string.IsNullOrWhiteSpace(reportJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MockInterviewReport>(reportJson, SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Stored mock interview report could not be read.");
            return null;
        }
    }

    /// <summary>
    /// Runs a JSON-mode call, retrying once with a stricter instruction before giving up,
    /// so a single malformed reply never surfaces as a broken page.
    /// </summary>
    private async Task<T?> GenerateJsonAsync<T>(
        string systemPrompt,
        string userPrompt,
        IntegrationFeature feature,
        Guid userId,
        CancellationToken ct)
        where T : class
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var prompt = attempt == 1 ? systemPrompt : systemPrompt + StrictJsonReminder;

            try
            {
                var response = await _gemini.GenerateAsync(prompt, userPrompt, feature, userId, jsonMode: true, ct);
                return JsonSerializer.Deserialize<T>(StripCodeFence(response.Content), SerializerOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "{Feature} returned unparseable JSON on attempt {Attempt}.", feature, attempt);
            }
            catch (AiServiceException ex)
            {
                _logger.LogWarning(ex, "{Feature} is unavailable.", feature);
                return null;
            }
        }

        return null;
    }

    /// <summary>Models sometimes wrap JSON in a fenced block even in JSON mode.</summary>
    private static string StripCodeFence(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
        {
            return trimmed;
        }

        var body = trimmed[(firstNewline + 1)..];
        var closing = body.LastIndexOf("```", StringComparison.Ordinal);
        return (closing >= 0 ? body[..closing] : body).Trim();
    }

    private async Task<Guid> ResolveUserIdAsync(Guid studentId, CancellationToken ct)
    {
        var student = await _students.GetByIdAsync(studentId, ct);
        return student?.UserId ?? Guid.Empty;
    }
}
