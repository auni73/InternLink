using InternLink.Web.Models;

namespace InternLink.Web.Repositories.Interface;

public interface IMockInterviewRepository
{
    Task CreateSessionAsync(MockInterviewSession session, CancellationToken ct = default);

    /// <summary>Scoped by student, so another student's session is indistinguishable from a missing one.</summary>
    Task<MockInterviewSession?> GetSessionAsync(Guid sessionId, Guid studentId, CancellationToken ct = default);

    Task<bool> UpdateTranscriptAsync(Guid sessionId, Guid studentId, string transcriptJson, CancellationToken ct = default);

    /// <summary>Writes the report and flips the session to Completed in one statement.</summary>
    Task<bool> CompleteSessionAsync(Guid sessionId, Guid studentId, string reportJson, CancellationToken ct = default);

    Task<IReadOnlyList<MockInterviewSession>> GetStudentSessionsAsync(Guid studentId, int take, CancellationToken ct = default);
}
