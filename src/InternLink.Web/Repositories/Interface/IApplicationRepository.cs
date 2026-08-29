using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Interface;

public interface IApplicationRepository
{
    Task<IReadOnlyList<Application>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default);
    Task<Application?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> HasAppliedAsync(Guid jobId, Guid studentId, CancellationToken ct = default);
    Task<Application> ApplyAsync(Guid jobId, Guid studentId, Guid resumeId, string? coverLetter, CancellationToken ct = default);
    Task<IReadOnlyList<StudentApplicationItemViewModel>> GetStudentApplicationsWithDetailsAsync(Guid studentId, CancellationToken ct = default);

    /// <summary>Writes the final cover letter onto the caller's own application. False when they have not applied.</summary>
    Task<bool> UpdateCoverLetterAsync(Guid jobId, Guid studentId, string coverLetterText, CancellationToken ct = default);

    // ATS Company Pipeline queries & guarded transitions
    Task<IReadOnlyList<JobFilterOptionDto>> GetCompanyJobFilterOptionsAsync(Guid companyId, CancellationToken ct = default);
    Task<IReadOnlyList<CompanyAtsApplicantItemViewModel>> GetCompanyAtsApplicationsAsync(Guid companyId, Guid? jobId, CancellationToken ct = default);
    Task<CompanyAtsApplicantDetailViewModel?> GetCompanyApplicationDetailAsync(Guid applicationId, Guid companyId, CancellationToken ct = default);
    Task<(bool Success, string? ErrorMessage)> TransitionApplicationStatusAsync(Guid applicationId, Guid companyId, ApplicationStatus newStatus, DateTimeOffset? scheduledDateTime, string? contextMeetingLink, CancellationToken ct = default);
    Task<(Stream? Stream, string FileName)> OpenAuthorizedResumeStreamForCompanyAsync(Guid applicationId, Guid companyId, CancellationToken ct = default);
}
