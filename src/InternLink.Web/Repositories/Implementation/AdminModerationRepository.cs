using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Implementation;

public class AdminModerationRepository : IAdminModerationRepository
{
    private readonly ApplicationDbContext _db;

    public AdminModerationRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<AdminUserItemViewModel> Users, int TotalCount, int StudentCount, int CompanyCount, int TotalAllCount)> GetUsersAsync(
        string? roleFilter, 
        string? searchQuery, 
        int page, 
        int pageSize, 
        CancellationToken ct = default)
    {
        var offset = Math.Max(0, (page - 1) * pageSize);

        string? normalizedRole = roleFilter?.Trim();
        if (string.Equals(normalizedRole, "Students", StringComparison.OrdinalIgnoreCase) || string.Equals(normalizedRole, "Student", StringComparison.OrdinalIgnoreCase))
        {
            normalizedRole = "Student";
        }
        else if (string.Equals(normalizedRole, "Companies", StringComparison.OrdinalIgnoreCase) || string.Equals(normalizedRole, "Company", StringComparison.OrdinalIgnoreCase))
        {
            normalizedRole = "Company";
        }
        else
        {
            normalizedRole = null;
        }

        string? normalizedSearch = string.IsNullOrWhiteSpace(searchQuery) ? null : $"%{searchQuery.Trim()}%";

        var roleParam = new SqlParameter("@role", SqlDbType.NVarChar, 50)
        {
            Value = (object?)normalizedRole ?? DBNull.Value
        };
        var searchParam = new SqlParameter("@kw", SqlDbType.NVarChar, 200)
        {
            Value = (object?)normalizedSearch ?? DBNull.Value
        };

        // 1. Overall Role Counts
        const string countSql = @"
            SELECT 
                COUNT(DISTINCT u.Id) AS TotalAll,
                SUM(CASE WHEN r.Name = 'Student' THEN 1 ELSE 0 END) AS TotalStudents,
                SUM(CASE WHEN r.Name = 'Company' THEN 1 ELSE 0 END) AS TotalCompanies
            FROM dbo.AspNetUsers u
            LEFT JOIN dbo.AspNetUserRoles ur ON u.Id = ur.UserId
            LEFT JOIN dbo.AspNetRoles r ON ur.RoleId = r.Id";

        var roleCounts = await _db.Database
            .SqlQueryRaw<UserRoleCountRowResult>(countSql)
            .FirstOrDefaultAsync(ct);

        int totalAll = roleCounts?.TotalAll ?? 0;
        int totalStudents = roleCounts?.TotalStudents ?? 0;
        int totalCompanies = roleCounts?.TotalCompanies ?? 0;

        // 2. Filtered Count
        const string filteredCountSql = @"
            SELECT COUNT(DISTINCT u.Id) AS Value
            FROM dbo.AspNetUsers u
            LEFT JOIN dbo.AspNetUserRoles ur ON u.Id = ur.UserId
            LEFT JOIN dbo.AspNetRoles r ON ur.RoleId = r.Id
            LEFT JOIN dbo.Students s ON u.Id = s.UserId
            LEFT JOIN dbo.Companies c ON u.Id = c.UserId
            WHERE (@role IS NULL OR r.Name = @role)
              AND (@kw IS NULL OR u.Email LIKE @kw OR u.UserName LIKE @kw OR s.FirstName LIKE @kw OR s.LastName LIKE @kw OR c.CompanyName LIKE @kw)";

        var totalCount = await _db.Database
            .SqlQueryRaw<int>(filteredCountSql, roleParam, searchParam)
            .FirstOrDefaultAsync(ct);

        if (totalCount == 0)
        {
            return (Array.Empty<AdminUserItemViewModel>(), 0, totalStudents, totalCompanies, totalAll);
        }

        // 3. Paginated Data Query
        var roleParam2 = (SqlParameter)((ICloneable)roleParam).Clone();
        var searchParam2 = (SqlParameter)((ICloneable)searchParam).Clone();
        var offsetParam = new SqlParameter("@offset", SqlDbType.Int) { Value = offset };
        var pageSizeParam = new SqlParameter("@pageSize", SqlDbType.Int) { Value = pageSize };

        const string dataSql = @"
            SELECT 
                u.Id AS UserId,
                u.Email,
                u.IsActive,
                u.CreatedAt,
                ISNULL(r.Name, 'Unassigned') AS Role,
                CASE 
                    WHEN r.Name = 'Student' AND s.Id IS NOT NULL THEN (s.FirstName + ' ' + s.LastName)
                    WHEN r.Name = 'Company' AND c.Id IS NOT NULL THEN c.CompanyName
                    ELSE ISNULL(u.UserName, u.Email)
                END AS DisplayName,
                CASE 
                    WHEN r.Name = 'Student' AND s.Id IS NOT NULL THEN ('Dept: ' + s.Department + ' | CGPA: ' + CAST(s.CGPA AS NVARCHAR(10)))
                    WHEN r.Name = 'Company' AND c.Id IS NOT NULL THEN ('Sector: ' + c.IndustrySector)
                    ELSE NULL
                END AS DetailSummary
            FROM dbo.AspNetUsers u
            LEFT JOIN dbo.AspNetUserRoles ur ON u.Id = ur.UserId
            LEFT JOIN dbo.AspNetRoles r ON ur.RoleId = r.Id
            LEFT JOIN dbo.Students s ON u.Id = s.UserId
            LEFT JOIN dbo.Companies c ON u.Id = c.UserId
            WHERE (@role IS NULL OR r.Name = @role)
              AND (@kw IS NULL OR u.Email LIKE @kw OR u.UserName LIKE @kw OR s.FirstName LIKE @kw OR s.LastName LIKE @kw OR c.CompanyName LIKE @kw)
            ORDER BY u.CreatedAt DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var rows = await _db.Database
            .SqlQueryRaw<AdminUserRowResult>(dataSql, roleParam2, searchParam2, offsetParam, pageSizeParam)
            .ToListAsync(ct);

        var items = rows.Select(r => new AdminUserItemViewModel
        {
            UserId = r.UserId,
            Email = r.Email ?? string.Empty,
            DisplayName = r.DisplayName,
            Role = r.Role,
            IsActive = r.IsActive,
            CreatedAt = r.CreatedAt,
            DetailSummary = r.DetailSummary
        }).ToList();

        return (items, totalCount, totalStudents, totalCompanies, totalAll);
    }

    public async Task<bool> SetUserActiveStatusAsync(Guid userId, bool isActive, CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@userId", SqlDbType.UniqueIdentifier) { Value = userId };
        var activeParam = new SqlParameter("@isActive", SqlDbType.Bit) { Value = isActive };

        const string sql = "UPDATE dbo.AspNetUsers SET IsActive = @isActive WHERE Id = @userId";

        var rows = await _db.Database.ExecuteSqlRawAsync(sql, new object[] { activeParam, idParam }, ct);
        return rows > 0;
    }

    public async Task<(IReadOnlyList<AdminCompanyQueueItemViewModel> Companies, int PendingCount, int VerifiedCount, int RejectedCount)> GetCompaniesQueueAsync(
        VerificationStatus? statusFilter, 
        CancellationToken ct = default)
    {
        var statusParam = new SqlParameter("@status", SqlDbType.TinyInt)
        {
            Value = statusFilter.HasValue ? (object)(byte)statusFilter.Value : DBNull.Value
        };

        // 1. Status Counts
        const string countSql = @"
            SELECT 
                SUM(CASE WHEN c.VerificationStatus = 0 THEN 1 ELSE 0 END) AS PendingCount,
                SUM(CASE WHEN c.VerificationStatus = 1 THEN 1 ELSE 0 END) AS VerifiedCount,
                SUM(CASE WHEN c.VerificationStatus = 2 THEN 1 ELSE 0 END) AS RejectedCount
            FROM dbo.Companies c";

        var counts = await _db.Database
            .SqlQueryRaw<CompanyStatusCountRowResult>(countSql)
            .FirstOrDefaultAsync(ct);

        int pendingCount = counts?.PendingCount ?? 0;
        int verifiedCount = counts?.VerifiedCount ?? 0;
        int rejectedCount = counts?.RejectedCount ?? 0;

        // 2. Data Query
        const string dataSql = @"
            SELECT 
                c.Id AS CompanyId,
                c.UserId,
                c.CompanyName,
                u.Email,
                c.CorporateWebsite,
                c.IndustrySector,
                c.VerificationStatus,
                c.AdminRejectionReason,
                c.CreatedAt,
                (SELECT COUNT(1) FROM dbo.Jobs j WHERE j.CompanyId = c.Id) AS JobCount
            FROM dbo.Companies c
            INNER JOIN dbo.AspNetUsers u ON c.UserId = u.Id
            WHERE (@status IS NULL OR c.VerificationStatus = @status)
            ORDER BY c.CreatedAt DESC";

        var rows = await _db.Database
            .SqlQueryRaw<AdminCompanyRowResult>(dataSql, statusParam)
            .ToListAsync(ct);

        var items = rows.Select(r => new AdminCompanyQueueItemViewModel
        {
            CompanyId = r.CompanyId,
            UserId = r.UserId,
            CompanyName = r.CompanyName,
            Email = r.Email ?? string.Empty,
            CorporateWebsite = r.CorporateWebsite,
            IndustrySector = r.IndustrySector,
            VerificationStatus = (VerificationStatus)r.VerificationStatus,
            AdminRejectionReason = r.AdminRejectionReason,
            CreatedAt = r.CreatedAt,
            JobCount = r.JobCount
        }).ToList();

        return (items, pendingCount, verifiedCount, rejectedCount);
    }

    public async Task<bool> ApproveCompanyAsync(Guid companyId, CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId };

        const string sql = @"
            UPDATE dbo.Companies 
            SET VerificationStatus = 1, 
                AdminRejectionReason = NULL 
            WHERE Id = @companyId";

        var rows = await _db.Database.ExecuteSqlRawAsync(sql, new object[] { idParam }, ct);
        return rows > 0;
    }

    public async Task<bool> RejectCompanyAsync(Guid companyId, string? reason, CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId };
        var reasonParam = new SqlParameter("@reason", SqlDbType.NVarChar, 500)
        {
            Value = string.IsNullOrWhiteSpace(reason) ? DBNull.Value : reason.Trim()
        };

        const string sql = @"
            UPDATE dbo.Companies 
            SET VerificationStatus = 2, 
                AdminRejectionReason = @reason 
            WHERE Id = @companyId";

        var rows = await _db.Database.ExecuteSqlRawAsync(sql, new object[] { idParam, reasonParam }, ct);
        return rows > 0;
    }

    public async Task<(IReadOnlyList<AdminJobQueueItemViewModel> Jobs, int PendingCount, int ApprovedCount)> GetJobsQueueAsync(
        bool? approvedFilter, 
        CancellationToken ct = default)
    {
        var approvedParam = new SqlParameter("@approved", SqlDbType.Bit)
        {
            Value = approvedFilter.HasValue ? (object)approvedFilter.Value : DBNull.Value
        };

        // 1. Status Counts (active non-closed jobs)
        const string countSql = @"
            SELECT 
                SUM(CASE WHEN j.IsApproved = 0 THEN 1 ELSE 0 END) AS PendingCount,
                SUM(CASE WHEN j.IsApproved = 1 THEN 1 ELSE 0 END) AS ApprovedCount
            FROM dbo.Jobs j
            WHERE j.IsClosed = 0";

        var counts = await _db.Database
            .SqlQueryRaw<JobStatusCountRowResult>(countSql)
            .FirstOrDefaultAsync(ct);

        int pendingCount = counts?.PendingCount ?? 0;
        int approvedCount = counts?.ApprovedCount ?? 0;

        // 2. Data Query
        const string dataSql = @"
            SELECT 
                j.Id AS JobId,
                j.CompanyId,
                c.CompanyName,
                c.CorporateWebsite,
                j.Title,
                j.LocationType,
                j.DeadLine,
                j.IsApproved,
                j.IsClosed,
                j.CoreDescription,
                j.SelectionCriteria,
                j.CreatedAt,
                (SELECT COUNT(1) FROM dbo.Applications a WHERE a.JobId = j.Id) AS ApplicantCount
            FROM dbo.Jobs j
            INNER JOIN dbo.Companies c ON j.CompanyId = c.Id
            WHERE (@approved IS NULL OR j.IsApproved = @approved) 
              AND j.IsClosed = 0
            ORDER BY j.CreatedAt DESC";

        var rows = await _db.Database
            .SqlQueryRaw<AdminJobRowResult>(dataSql, approvedParam)
            .ToListAsync(ct);

        var items = rows.Select(r => new AdminJobQueueItemViewModel
        {
            JobId = r.JobId,
            CompanyId = r.CompanyId,
            CompanyName = r.CompanyName,
            CorporateWebsite = r.CorporateWebsite,
            Title = r.Title,
            LocationType = (LocationType)r.LocationType,
            DeadLine = r.DeadLine,
            IsApproved = r.IsApproved,
            IsClosed = r.IsClosed,
            CoreDescription = r.CoreDescription,
            SelectionCriteria = r.SelectionCriteria,
            CreatedAt = r.CreatedAt,
            ApplicantCount = r.ApplicantCount
        }).ToList();

        return (items, pendingCount, approvedCount);
    }

    public async Task<bool> ApproveJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@jobId", SqlDbType.UniqueIdentifier) { Value = jobId };

        const string sql = "UPDATE dbo.Jobs SET IsApproved = 1 WHERE Id = @jobId";

        var rows = await _db.Database.ExecuteSqlRawAsync(sql, new object[] { idParam }, ct);
        return rows > 0;
    }
}

// Row Result POCOs for SQL mapping
public class UserRoleCountRowResult
{
    public int TotalAll { get; set; }
    public int TotalStudents { get; set; }
    public int TotalCompanies { get; set; }
}

public class AdminUserRowResult
{
    public Guid UserId { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Role { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? DetailSummary { get; set; }
}

public class CompanyStatusCountRowResult
{
    public int PendingCount { get; set; }
    public int VerifiedCount { get; set; }
    public int RejectedCount { get; set; }
}

public class AdminCompanyRowResult
{
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? CorporateWebsite { get; set; }
    public string IndustrySector { get; set; } = string.Empty;
    public byte VerificationStatus { get; set; }
    public string? AdminRejectionReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int JobCount { get; set; }
}

public class JobStatusCountRowResult
{
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
}

public class AdminJobRowResult
{
    public Guid JobId { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CorporateWebsite { get; set; }
    public string Title { get; set; } = string.Empty;
    public byte LocationType { get; set; }
    public DateTimeOffset DeadLine { get; set; }
    public bool IsApproved { get; set; }
    public bool IsClosed { get; set; }
    public string CoreDescription { get; set; } = string.Empty;
    public string? SelectionCriteria { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int ApplicantCount { get; set; }
}
