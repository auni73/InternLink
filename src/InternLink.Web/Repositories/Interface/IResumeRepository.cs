using InternLink.Web.Models;

namespace InternLink.Web.Repositories.Interface;

public interface IResumeRepository
{
    Task<Resume?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Resume>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default);
    Task<Resume> CreateAsync(Guid studentId, CancellationToken ct = default);
    Task UpdateDynamicJsonDataAsync(Guid id, string dynamicJsonData, DateTimeOffset lastModified, CancellationToken ct = default);
    Task FinalizeAsync(Guid id, string documentPath, DateTimeOffset lastModified, CancellationToken ct = default);
    Task<int> GetFinalizedCountByStudentIdAsync(Guid studentId, CancellationToken ct = default);
}
