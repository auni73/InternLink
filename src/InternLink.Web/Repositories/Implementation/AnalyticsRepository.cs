using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Implementation;

public class AnalyticsRepository : IAnalyticsRepository
{
    private readonly ApplicationDbContext _db;

    public AnalyticsRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AdminAnalyticsViewModel> GetAdminAnalyticsAsync(CancellationToken ct = default)
    {
        // 1. High-level platform KPIs (single aggregated read)
        const string kpiSql = @"
            SELECT 
                (SELECT COUNT(1) FROM dbo.Students s INNER JOIN dbo.AspNetUsers u ON s.UserId = u.Id WHERE u.IsActive = 1) AS ActiveStudentCount,
                (SELECT COUNT(1) FROM dbo.Companies c INNER JOIN dbo.AspNetUsers u ON c.UserId = u.Id WHERE u.IsActive = 1 AND c.VerificationStatus = 1) AS ActiveCompanyCount,
                (SELECT COUNT(1) FROM dbo.Jobs j WHERE j.IsApproved = 1 AND j.IsClosed = 0 AND j.DeadLine >= SYSDATETIMEOFFSET()) AS OpenJobCount,
                (SELECT COUNT(1) FROM dbo.Applications) AS TotalApplicationsCount,
                (SELECT COUNT(1) FROM dbo.Interviews) AS TotalInterviewsCount,
                (SELECT COUNT(1) FROM dbo.Assessments WHERE AchievedScore >= 70) AS VerifiedSkillsEarnedCount";

        var kpiRow = await _db.Database
            .SqlQueryRaw<PlatformKpiRowResult>(kpiSql)
            .FirstOrDefaultAsync(ct);

        // 2. Application Pipeline Distribution (GROUP BY ApplicationStatus)
        const string statusSql = @"
            SELECT 
                a.ApplicationStatus AS StatusValue,
                COUNT(1) AS StatusCount
            FROM dbo.Applications a
            GROUP BY a.ApplicationStatus";

        var statusRows = await _db.Database
            .SqlQueryRaw<ApplicationStatusGroupRowResult>(statusSql)
            .ToListAsync(ct);

        var statusDict = new Dictionary<string, int>
        {
            { "Applied", 0 },
            { "Screened", 0 },
            { "Scheduled", 0 },
            { "Offered", 0 },
            { "Rejected", 0 }
        };

        foreach (var row in statusRows)
        {
            var statusEnum = (ApplicationStatus)row.StatusValue;
            var key = statusEnum.ToString();
            if (statusDict.ContainsKey(key))
            {
                statusDict[key] = row.StatusCount;
            }
        }

        // 3. 7-Day Continuous Application Velocity (Recursive CTE + LEFT JOIN dbo.Applications)
        // Ensures days with 0 submissions are represented as 0 rather than skipped.
        const string trendSql = @"
            WITH DateRange AS (
                SELECT CAST(DATEADD(DAY, -6, SYSDATETIMEOFFSET()) AS date) AS DateVal
                UNION ALL
                SELECT DATEADD(DAY, 1, DateVal)
                FROM DateRange
                WHERE DateVal < CAST(SYSDATETIMEOFFSET() AS date)
            )
            SELECT 
                d.DateVal AS MetricDate,
                COUNT(a.Id) AS ApplicationCount
            FROM DateRange d
            LEFT JOIN dbo.Applications a 
                ON CAST(a.SubmittedAt AS date) = d.DateVal
            GROUP BY d.DateVal
            ORDER BY d.DateVal ASC";

        var trendRows = await _db.Database
            .SqlQueryRaw<DailyApplicationTrendRowResult>(trendSql)
            .ToListAsync(ct);

        var dailyMetrics = trendRows.Select(r => new DailyApplicationMetric
        {
            Date = r.MetricDate.ToString("yyyy-MM-dd"),
            FormattedDate = r.MetricDate.ToString("MMM dd"),
            Count = r.ApplicationCount
        }).ToList();

        var viewModel = new AdminAnalyticsViewModel
        {
            ActiveStudentCount = kpiRow?.ActiveStudentCount ?? 0,
            ActiveCompanyCount = kpiRow?.ActiveCompanyCount ?? 0,
            OpenJobCount = kpiRow?.OpenJobCount ?? 0,
            TotalApplicationsCount = kpiRow?.TotalApplicationsCount ?? 0,
            TotalInterviewsCount = kpiRow?.TotalInterviewsCount ?? 0,
            VerifiedSkillsEarnedCount = kpiRow?.VerifiedSkillsEarnedCount ?? 0,
            ApplicationsByStatus = statusDict,
            NewApplicationsLast7Days = dailyMetrics
        };

        // Serialize client payload for <script type="application/json"> data island
        viewModel.JsonPayload = JsonSerializer.Serialize(new
        {
            kpis = new
            {
                activeStudents = viewModel.ActiveStudentCount,
                activeCompanies = viewModel.ActiveCompanyCount,
                openJobs = viewModel.OpenJobCount,
                totalApplications = viewModel.TotalApplicationsCount,
                totalInterviews = viewModel.TotalInterviewsCount,
                verifiedSkills = viewModel.VerifiedSkillsEarnedCount
            },
            statusBreakdown = viewModel.ApplicationsByStatus,
            dailyTrend = viewModel.NewApplicationsLast7Days.Select(d => new
            {
                date = d.Date,
                formattedDate = d.FormattedDate,
                count = d.Count
            })
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        return viewModel;
    }
}

// SQL Mapping POCOs
public class PlatformKpiRowResult
{
    public int ActiveStudentCount { get; set; }
    public int ActiveCompanyCount { get; set; }
    public int OpenJobCount { get; set; }
    public int TotalApplicationsCount { get; set; }
    public int TotalInterviewsCount { get; set; }
    public int VerifiedSkillsEarnedCount { get; set; }
}

public class ApplicationStatusGroupRowResult
{
    public byte StatusValue { get; set; }
    public int StatusCount { get; set; }
}

public class DailyApplicationTrendRowResult
{
    public DateOnly MetricDate { get; set; }
    public int ApplicationCount { get; set; }
}
