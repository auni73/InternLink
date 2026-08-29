namespace InternLink.Web.Services.CoverLetter;

public interface ICoverLetterService
{
    /// <summary>Generates letter body prose for a job. Returns null when the student or job cannot be resolved.</summary>
    Task<string?> GenerateAsync(Guid studentId, Guid jobId, CancellationToken ct = default);

    /// <summary>
    /// Persists the final text onto the student's existing application for this job.
    /// Returns false when they have not applied yet.
    /// </summary>
    Task<bool> SaveToApplicationAsync(Guid studentId, Guid jobId, string finalText, CancellationToken ct = default);
}
