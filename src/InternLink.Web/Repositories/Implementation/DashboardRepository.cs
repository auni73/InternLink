using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Implementation;

// Each stats method is deliberately ONE round-trip: correlated COUNT subqueries in a single SELECT.
public class DashboardRepository : IDashboardRepository
{
    private readonly ApplicationDbContext _db;

    public DashboardRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<StudentDashboardViewModel> GetStudentStatsAsync(Guid studentId, CancellationToken ct = default)
    {
        var sidParam = new SqlParameter("@sid", SqlDbType.UniqueIdentifier) { Value = studentId };

        const string sql = @"
            SELECT
                (SELECT COUNT(*) FROM dbo.Applications a WHERE a.StudentId = @sid) AS ApplicationsCount,
                (SELECT COUNT(*) FROM dbo.Resumes r WHERE r.StudentId = @sid AND r.DocumentPath IS NOT NULL) AS FinalizedResumesCount,
                (SELECT COUNT(DISTINCT ass.SkillId) FROM dbo.Assessments ass WHERE ass.StudentId = @sid) AS VerifiedSkillsCount";

        return await _db.Database
            .SqlQueryRaw<StudentDashboardViewModel>(sql, sidParam)
            .FirstAsync(ct);
    }

    public async Task<CompanyDashboardViewModel> GetCompanyStatsAsync(Guid companyId, CancellationToken ct = default)
    {
        var cidParam = new SqlParameter("@cid", SqlDbType.UniqueIdentifier) { Value = companyId };

        const string sql = @"
            SELECT
                (SELECT COUNT(*) FROM dbo.Jobs j
                 WHERE j.CompanyId = @cid AND j.IsApproved = 1 AND j.IsClosed = 0 AND j.DeadLine >= SYSDATETIMEOFFSET()) AS OpenJobsCount,
                (SELECT COUNT(*) FROM dbo.Applications a
                 INNER JOIN dbo.Jobs j ON j.Id = a.JobId
                 WHERE j.CompanyId = @cid) AS TotalApplicantsCount";

        return await _db.Database
            .SqlQueryRaw<CompanyDashboardViewModel>(sql, cidParam)
            .FirstAsync(ct);
    }

    public async Task<AdminDashboardViewModel> GetAdminStatsAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
                (SELECT COUNT(*) FROM dbo.Companies c WHERE c.VerificationStatus = 0) AS PendingCompaniesCount,
                (SELECT COUNT(*) FROM dbo.Jobs j WHERE j.IsApproved = 0 AND j.IsClosed = 0) AS PendingJobsCount";

        return await _db.Database
            .SqlQueryRaw<AdminDashboardViewModel>(sql)
            .FirstAsync(ct);
    }

    public async Task<CounselorDashboardViewModel> GetCounselorStatsAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT (SELECT COUNT(*) FROM dbo.Students) AS StudentCount";

        return await _db.Database
            .SqlQueryRaw<CounselorDashboardViewModel>(sql)
            .FirstAsync(ct);
    }
}
