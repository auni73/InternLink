using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Resume;

public interface IResumeService
{
    Task<IReadOnlyList<ResumeItemViewModel>> GetStudentResumesAsync(Guid studentId, CancellationToken ct = default);
    Task<ResumeBuilderViewModel?> GetResumeForEditAsync(Guid resumeId, Guid studentId, CancellationToken ct = default);
    Task<Models.Resume> CreateDraftResumeAsync(Guid studentId, CancellationToken ct = default);
    Task<bool> UpdateStepAsync(Guid resumeId, Guid studentId, string stepName, string stepJson, CancellationToken ct = default);
    Task<(bool Success, string? DocumentPath, string? ErrorMessage)> FinalizeResumeAsync(Guid resumeId, Guid studentId, CancellationToken ct = default);
    Task<(Stream? Stream, string FileName)> OpenDownloadStreamAsync(Guid resumeId, Guid studentId, CancellationToken ct = default);
}
