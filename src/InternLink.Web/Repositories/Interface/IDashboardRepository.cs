using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Interface;

public interface IDashboardRepository
{
    Task<StudentDashboardViewModel> GetStudentStatsAsync(Guid studentId, CancellationToken ct = default);
    Task<CompanyDashboardViewModel> GetCompanyStatsAsync(Guid companyId, CancellationToken ct = default);
    Task<AdminDashboardViewModel> GetAdminStatsAsync(CancellationToken ct = default);
    Task<CounselorDashboardViewModel> GetCounselorStatsAsync(CancellationToken ct = default);
}
