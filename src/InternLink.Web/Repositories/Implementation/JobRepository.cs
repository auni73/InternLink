using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;

namespace InternLink.Web.Repositories.Implementation;

public class JobRepository : IJobRepository
{
    private readonly ApplicationDbContext _db;

    public JobRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Job>> GetApprovedOpenJobsAsync(
        LocationType? locationType, 
        int page, 
        int pageSize, 
        CancellationToken ct = default)
    {
        var offset = Math.Max(0, (page - 1) * pageSize);
        var locationTypeParam = new SqlParameter("@lt", SqlDbType.TinyInt)
        {
            Value = locationType.HasValue ? (object)(byte)locationType.Value : DBNull.Value
        };
        var offsetParam = new SqlParameter("@offset", SqlDbType.Int) { Value = offset };
        var pageSizeParam = new SqlParameter("@pageSize", SqlDbType.Int) { Value = pageSize };

        const string sql = @"
            SELECT j.* 
            FROM dbo.Jobs j 
            WHERE j.IsApproved = 1 
              AND j.IsClosed = 0 
              AND j.DeadLine >= SYSDATETIMEOFFSET() 
              AND (@lt IS NULL OR j.LocationType = @lt) 
            ORDER BY j.DeadLine ASC 
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        return await _db.Jobs
            .FromSqlRaw(sql, locationTypeParam, offsetParam, pageSizeParam)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<Job?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id };
        const string sql = "SELECT j.* FROM dbo.Jobs j WHERE j.Id = @id";

        return await _db.Jobs
            .FromSqlRaw(sql, idParam)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> GetApprovedOpenJobsCountAsync(LocationType? locationType, CancellationToken ct = default)
    {
        var locationTypeParam = new SqlParameter("@lt", SqlDbType.TinyInt)
        {
            Value = locationType.HasValue ? (object)(byte)locationType.Value : DBNull.Value
        };

        const string sql = @"
            SELECT COUNT(*) AS Value 
            FROM dbo.Jobs j 
            WHERE j.IsApproved = 1 
              AND j.IsClosed = 0 
              AND j.DeadLine >= SYSDATETIMEOFFSET() 
              AND (@lt IS NULL OR j.LocationType = @lt)";

        return await _db.Database
            .SqlQueryRaw<int>(sql, locationTypeParam)
            .FirstOrDefaultAsync(ct);
    }
}
