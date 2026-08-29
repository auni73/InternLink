using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Interface;

public interface ICounselorRepository
{
    Task<(IReadOnlyList<CounselorStudentDirectoryItemViewModel> Items, int TotalCount)> GetStudentDirectoryAsync(
        string? search, 
        int page, 
        int pageSize, 
        CancellationToken ct = default);

    Task<IReadOnlyList<CounselorFeedbackItemViewModel>> GetCounselorFeedbacksByStudentIdAsync(
        Guid studentId, 
        CancellationToken ct = default);

    Task<IReadOnlyList<CounselorFeedbackItemViewModel>> GetAdvisingNotesForStudentUserAsync(
        Guid studentUserId, 
        CancellationToken ct = default);

    Task<Guid> AddCounselorFeedbackAsync(
        Guid studentId, 
        Guid counselorUserId, 
        string narrativeMarkdown, 
        DateTimeOffset meetingDate, 
        CancellationToken ct = default);
}
