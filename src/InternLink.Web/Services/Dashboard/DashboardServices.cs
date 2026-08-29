using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Dashboard;

public interface IStudentDashboardService
{
    Task<StudentDashboardViewModel> GetAsync(Guid? studentId, CancellationToken ct = default);
}

public interface ICompanyDashboardService
{
    Task<CompanyDashboardViewModel> GetAsync(Guid? companyId, CancellationToken ct = default);
}

public interface IAdminDashboardService
{
    Task<AdminDashboardViewModel> GetAsync(CancellationToken ct = default);
}

public interface ICounselorDashboardService
{
    Task<CounselorDashboardViewModel> GetAsync(CancellationToken ct = default);
}

public sealed class StudentDashboardService : IStudentDashboardService
{
    private readonly IDashboardRepository _repository;
    public StudentDashboardService(IDashboardRepository repository) => _repository = repository;

    // Null studentId (no profile row) renders an all-zero dashboard rather than failing.
    public async Task<StudentDashboardViewModel> GetAsync(Guid? studentId, CancellationToken ct = default)
        => studentId is null ? new StudentDashboardViewModel() : await _repository.GetStudentStatsAsync(studentId.Value, ct);
}

public sealed class CompanyDashboardService : ICompanyDashboardService
{
    private readonly IDashboardRepository _repository;
    public CompanyDashboardService(IDashboardRepository repository) => _repository = repository;

    public async Task<CompanyDashboardViewModel> GetAsync(Guid? companyId, CancellationToken ct = default)
        => companyId is null ? new CompanyDashboardViewModel() : await _repository.GetCompanyStatsAsync(companyId.Value, ct);
}

public sealed class AdminDashboardService : IAdminDashboardService
{
    private readonly IDashboardRepository _repository;
    public AdminDashboardService(IDashboardRepository repository) => _repository = repository;

    public Task<AdminDashboardViewModel> GetAsync(CancellationToken ct = default)
        => _repository.GetAdminStatsAsync(ct);
}

public sealed class CounselorDashboardService : ICounselorDashboardService
{
    private readonly IDashboardRepository _repository;
    public CounselorDashboardService(IDashboardRepository repository) => _repository = repository;

    public Task<CounselorDashboardViewModel> GetAsync(CancellationToken ct = default)
        => _repository.GetCounselorStatsAsync(ct);
}
