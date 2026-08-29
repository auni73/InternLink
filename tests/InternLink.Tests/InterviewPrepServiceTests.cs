using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Services.AI;
using InternLink.Web.Services.InterviewPrep;
using InternLink.Web.ViewModels;
using Xunit;

namespace InternLink.Tests;

public class InterviewPrepServiceTests
{
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid StudentUserId = Guid.NewGuid();
    private static readonly Guid OtherStudentId = Guid.NewGuid();
    private static readonly Guid JobId = Guid.NewGuid();

    // ---- Question bank ----

    [Fact]
    public async Task QuestionBank_ReturnsNormalizedCategories()
    {
        var harness = new Harness();
        harness.Gemini.Responses.Enqueue("""
            { "questions": [
                { "questionText": "Explain dependency injection.", "category": "technical" },
                { "questionText": "Why this company?", "category": "hr" },
                { "questionText": "A deadline slips. What do you do?", "category": "Behavioural" }
            ] }
            """);

        var questions = await harness.Service.GenerateQuestionsAsync(StudentId, "Backend Intern", jobId: null);

        Assert.Equal(3, questions.Count);
        Assert.Equal("Technical", questions[0].Category);
        Assert.Equal("HR", questions[1].Category);
        // Anything the model invents lands in Situational rather than creating a fourth tab.
        Assert.Equal("Situational", questions[2].Category);
        Assert.Equal(IntegrationFeature.QuestionBank, harness.Gemini.Features[0]);
    }

    [Fact]
    public async Task QuestionBank_TargetsThePostingWhenAJobIsChosen()
    {
        var harness = new Harness();
        harness.Gemini.Responses.Enqueue("""{ "questions": [] }""");

        await harness.Service.GenerateQuestionsAsync(StudentId, "Backend Intern", JobId);

        Assert.Contains("Junior .NET Developer Intern", harness.Gemini.UserPrompts[0]);
        Assert.Contains("Strong C# fundamentals", harness.Gemini.UserPrompts[0]);
    }

    [Fact]
    public async Task QuestionBank_ReturnsEmpty_WhenTheModelIsUnavailable()
    {
        var harness = new Harness();
        harness.Gemini.ThrowAiServiceException = true;

        var questions = await harness.Service.GenerateQuestionsAsync(StudentId, "Backend Intern", jobId: null);

        Assert.Empty(questions);
    }

    // ---- Starting a session ----

    [Fact]
    public async Task Start_PersistsTheOpeningQuestion()
    {
        var harness = new Harness();
        harness.Gemini.Responses.Enqueue("Tell me about a project you shipped.");

        var result = await harness.Service.StartSessionAsync(StudentId, "Backend Intern", jobId: null);

        Assert.NotNull(result);
        Assert.Equal("Tell me about a project you shipped.", result!.FirstQuestion);

        var stored = Assert.Single(harness.Sessions.Sessions);
        Assert.Equal(MockInterviewStatus.InProgress, stored.Status);

        var transcript = Deserialize(stored.TranscriptJson);
        var turn = Assert.Single(transcript);
        Assert.Equal(TranscriptSpeakers.Interviewer, turn.Speaker);
        Assert.Equal("Tell me about a project you shipped.", turn.Text);
    }

    [Fact]
    public async Task Start_InstructsOneQuestionPerTurnAndNoBreakingCharacter()
    {
        var harness = new Harness();
        harness.Gemini.Responses.Enqueue("First question?");

        await harness.Service.StartSessionAsync(StudentId, "Backend Intern", jobId: null);

        var systemPrompt = harness.Gemini.SystemPrompts[0];
        Assert.Contains("exactly ONE question per turn", systemPrompt);
        Assert.Contains("Stay in character", systemPrompt);
    }

    [Fact]
    public async Task Start_ReturnsNull_AndStoresNothing_WhenTheModelIsUnavailable()
    {
        var harness = new Harness();
        harness.Gemini.ThrowAiServiceException = true;

        var result = await harness.Service.StartSessionAsync(StudentId, "Backend Intern", jobId: null);

        Assert.Null(result);
        Assert.Empty(harness.Sessions.Sessions);
    }

    // ---- Turn-taking ----

    [Fact]
    public async Task Message_SendsTheWholeHistorySoFollowUpsStayCoherent()
    {
        var harness = new Harness();
        var sessionId = await harness.StartAsync("First question?");

        harness.Gemini.Responses.Enqueue("Second question?");
        await harness.Service.SendMessageAsync(sessionId, StudentId, "I built a library system.");

        harness.Gemini.Responses.Enqueue("Third question?");
        await harness.Service.SendMessageAsync(sessionId, StudentId, "I used EF Core.");

        // Kickoff + 2 interviewer turns + 2 student turns: the third question does not exist yet.
        var lastHistory = harness.Gemini.Histories[^1];
        Assert.Equal(5, lastHistory.Count);
        Assert.Contains(lastHistory, m => m.Text == "I built a library system.");
        Assert.Contains(lastHistory, m => m.Text == "First question?");

        // Roles must alternate correctly or the model loses track of who said what.
        Assert.True(lastHistory[1].IsUser is false);
        Assert.True(lastHistory[2].IsUser);
    }

    [Fact]
    public async Task Message_PersistsEveryTurn_SoAClosedTabLosesNothing()
    {
        var harness = new Harness();
        var sessionId = await harness.StartAsync("First question?");

        harness.Gemini.Responses.Enqueue("Second question?");
        await harness.Service.SendMessageAsync(sessionId, StudentId, "My answer.");

        var transcript = Deserialize(harness.Sessions.Sessions[0].TranscriptJson);
        Assert.Equal(3, transcript.Count);
        Assert.Equal(TranscriptSpeakers.Student, transcript[1].Speaker);
        Assert.Equal("My answer.", transcript[1].Text);
        Assert.Equal("Second question?", transcript[2].Text);
    }

    [Fact]
    public async Task Message_KeepsTheStudentsAnswer_WhenTheModelFails()
    {
        var harness = new Harness();
        var sessionId = await harness.StartAsync("First question?");
        harness.Gemini.ThrowAiServiceException = true;

        var result = await harness.Service.SendMessageAsync(sessionId, StudentId, "My careful answer.");

        Assert.Equal(SendMessageOutcome.AiUnavailable, result.Outcome);

        var transcript = Deserialize(harness.Sessions.Sessions[0].TranscriptJson);
        Assert.Equal(2, transcript.Count);
        Assert.Equal("My careful answer.", transcript[1].Text);
    }

    [Fact]
    public async Task Message_TreatsAnotherStudentsSessionAsMissing()
    {
        var harness = new Harness();
        var sessionId = await harness.StartAsync("First question?");

        var result = await harness.Service.SendMessageAsync(sessionId, OtherStudentId, "Let me in.");

        Assert.Equal(SendMessageOutcome.SessionNotFound, result.Outcome);
        Assert.Single(harness.Gemini.SystemPrompts);
    }

    [Fact]
    public async Task Message_RefusesToAppendToACompletedInterview()
    {
        var harness = new Harness();
        var sessionId = await harness.StartAsync("First question?");
        harness.Gemini.Responses.Enqueue("""{ "accuracySummary": "Done." }""");
        await harness.Service.EndSessionAsync(sessionId, StudentId);

        var result = await harness.Service.SendMessageAsync(sessionId, StudentId, "One more thing.");

        Assert.Equal(SendMessageOutcome.SessionAlreadyCompleted, result.Outcome);
    }

    // ---- Ending and reporting ----

    [Fact]
    public async Task End_GroundsTheReportInTheTranscriptAndCompletesTheSession()
    {
        var harness = new Harness();
        var sessionId = await harness.StartAsync("Tell me about a project.");
        harness.Gemini.Responses.Enqueue("And how did you test it?");
        await harness.Service.SendMessageAsync(sessionId, StudentId, "I built a library system.");

        harness.Gemini.Responses.Enqueue("""
            { "accuracySummary": "You explained the library system clearly.",
              "logicGaps": ["No mention of testing."],
              "improvementSuggestions": ["Quantify the impact."] }
            """);

        var report = await harness.Service.EndSessionAsync(sessionId, StudentId);

        Assert.NotNull(report);
        Assert.Equal("You explained the library system clearly.", report!.AccuracySummary);
        Assert.Single(report.LogicGaps);

        // The whole transcript is handed to the reviewer, not just the last turn.
        var reportPrompt = harness.Gemini.UserPrompts[^1];
        Assert.Contains("Candidate: I built a library system.", reportPrompt);
        Assert.Contains("Interviewer: Tell me about a project.", reportPrompt);

        var stored = harness.Sessions.Sessions[0];
        Assert.Equal(MockInterviewStatus.Completed, stored.Status);
        Assert.NotNull(stored.CompletedAt);
        Assert.Contains("library system", stored.ReportJson);
    }

    [Fact]
    public async Task End_StillCompletesWithAFallbackReport_WhenTheModelIsUnavailable()
    {
        var harness = new Harness();
        var sessionId = await harness.StartAsync("First question?");
        harness.Gemini.ThrowAiServiceException = true;

        var report = await harness.Service.EndSessionAsync(sessionId, StudentId);

        Assert.NotNull(report);
        Assert.Contains("transcript is saved", report!.AccuracySummary);
        Assert.Equal(MockInterviewStatus.Completed, harness.Sessions.Sessions[0].Status);
    }

    [Fact]
    public async Task End_IsIdempotent_AndDoesNotPayForASecondReport()
    {
        var harness = new Harness();
        var sessionId = await harness.StartAsync("First question?");
        harness.Gemini.Responses.Enqueue("""{ "accuracySummary": "First run." }""");
        await harness.Service.EndSessionAsync(sessionId, StudentId);

        var callsAfterFirstEnd = harness.Gemini.SystemPrompts.Count;
        var report = await harness.Service.EndSessionAsync(sessionId, StudentId);

        Assert.Equal("First run.", report!.AccuracySummary);
        Assert.Equal(callsAfterFirstEnd, harness.Gemini.SystemPrompts.Count);
    }

    [Fact]
    public async Task End_TreatsAnotherStudentsSessionAsMissing()
    {
        var harness = new Harness();
        var sessionId = await harness.StartAsync("First question?");

        var report = await harness.Service.EndSessionAsync(sessionId, OtherStudentId);

        Assert.Null(report);
        Assert.Equal(MockInterviewStatus.InProgress, harness.Sessions.Sessions[0].Status);
    }

    // ---- Reading back ----

    [Fact]
    public async Task GetSession_ReturnsNull_ForAnotherStudent()
    {
        var harness = new Harness();
        var sessionId = await harness.StartAsync("First question?");

        Assert.Null(await harness.Service.GetSessionAsync(sessionId, OtherStudentId));
        Assert.NotNull(await harness.Service.GetSessionAsync(sessionId, StudentId));
    }

    [Fact]
    public async Task GetReport_SurvivesAnUnreadableStoredTranscript()
    {
        var harness = new Harness();
        var sessionId = await harness.StartAsync("First question?");
        harness.Sessions.Sessions[0].TranscriptJson = "not json";

        var report = await harness.Service.GetReportAsync(sessionId, StudentId);

        Assert.NotNull(report);
        Assert.Empty(report!.Transcript);
    }

    private static List<TranscriptTurn> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<TranscriptTurn>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

    private sealed class Harness
    {
        public RecordingChatGemini Gemini { get; } = new();
        public FakeMockInterviewRepository Sessions { get; } = new();
        public InterviewPrepService Service { get; }

        public Harness()
        {
            Service = new InterviewPrepService(
                Sessions,
                new FakeStudents(),
                new FakeJobs(),
                Gemini,
                NullLogger<InterviewPrepService>.Instance);
        }

        public async Task<Guid> StartAsync(string firstQuestion)
        {
            Gemini.Responses.Enqueue(firstQuestion);
            var result = await Service.StartSessionAsync(StudentId, "Backend Intern", jobId: null);
            return result!.SessionId;
        }
    }

    private sealed class FakeStudents : StubStudentRepository
    {
        public override Task<Student?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Student?>(new Student
            {
                Id = StudentId,
                UserId = StudentUserId,
                FirstName = "Tanvir",
                LastName = "Ahmed",
                Department = "Computer Science and Engineering"
            });
    }

    private sealed class FakeJobs : StubJobRepository
    {
        public override Task<Job?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Job?>(new Job
            {
                Id = JobId,
                Title = "Junior .NET Developer Intern",
                CoreDescription = "Build ASP.NET Core services.",
                SelectionCriteria = "Strong C# fundamentals."
            });
    }

    private sealed class RecordingChatGemini : IGeminiClient
    {
        public Queue<string> Responses { get; } = new();
        public List<string> SystemPrompts { get; } = [];
        public List<string> UserPrompts { get; } = [];
        public List<IReadOnlyList<ChatMessage>> Histories { get; } = [];
        public List<IntegrationFeature> Features { get; } = [];
        public bool ThrowAiServiceException { get; set; }

        public Task<GeminiResponse> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            IntegrationFeature feature,
            Guid userId,
            bool jsonMode,
            CancellationToken ct = default)
        {
            SystemPrompts.Add(systemPrompt);
            UserPrompts.Add(userPrompt);
            Features.Add(feature);

            if (ThrowAiServiceException)
            {
                throw new AiServiceException("busy");
            }

            return Task.FromResult(new GeminiResponse(Next(), 100, 50, 0.0002m));
        }

        public Task<GeminiResponse> GenerateChatAsync(
            string systemPrompt,
            IReadOnlyList<ChatMessage> history,
            IntegrationFeature feature,
            Guid userId,
            CancellationToken ct = default)
        {
            SystemPrompts.Add(systemPrompt);
            Histories.Add(history);
            Features.Add(feature);

            if (ThrowAiServiceException)
            {
                throw new AiServiceException("busy");
            }

            return Task.FromResult(new GeminiResponse(Next(), 100, 50, 0.0002m));
        }

        private string Next() => Responses.Count > 0 ? Responses.Dequeue() : "{}";
    }
}
