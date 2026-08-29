using InternLink.Web.Models;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Interface;

public interface IApplicationRepository
{
    Task<IReadOnlyList<Application>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default);
    Task<Application?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> HasAppliedAsync(Guid jobId, Guid studentId, CancellationToken ct = default);
    Task<Application> ApplyAsync(Guid jobId, Guid studentId, Guid resumeId, string? coverLetter, CancellationToken ct = default);
    Task<IReadOnlyList<StudentApplicationItemViewModel>> GetStudentApplicationsWithDetailsAsync(Guid studentId, CancellationToken ct = default);
}
