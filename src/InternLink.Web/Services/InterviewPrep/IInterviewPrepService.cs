using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.InterviewPrep;

public interface IInterviewPrepService
{
    /// <summary>Returns an empty list when the model is unavailable, so the page renders a retry state.</summary>
    Task<IReadOnlyList<InterviewQuestion>> GenerateQuestionsAsync(
        Guid studentId,
        string role,
        Guid? jobId,
        CancellationToken ct = default);

    Task<StartSessionResult?> StartSessionAsync(
        Guid studentId,
        string role,
        Guid? jobId,
        CancellationToken ct = default);

    Task<SendMessageResult> SendMessageAsync(
        Guid sessionId,
        Guid studentId,
        string studentReply,
        CancellationToken ct = default);

    Task<MockInterviewReport?> EndSessionAsync(Guid sessionId, Guid studentId, CancellationToken ct = default);

    Task<MockInterviewSessionViewModel?> GetSessionAsync(Guid sessionId, Guid studentId, CancellationToken ct = default);

    Task<MockInterviewReportViewModel?> GetReportAsync(Guid sessionId, Guid studentId, CancellationToken ct = default);

    Task<IReadOnlyList<MockInterviewSessionListItem>> GetRecentSessionsAsync(
        Guid studentId,
        int take,
        CancellationToken ct = default);
}

public sealed record StartSessionResult(Guid SessionId, string FirstQuestion);

public enum SendMessageOutcome
{
    Ok,
    SessionNotFound,
    SessionAlreadyCompleted,
    AiUnavailable
}

public sealed record SendMessageResult(SendMessageOutcome Outcome, string? AiReply = null);
