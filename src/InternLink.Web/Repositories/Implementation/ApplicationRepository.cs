using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;

namespace InternLink.Web.Repositories.Implementation;

public class ApplicationRepository : IApplicationRepository
{
    private readonly ApplicationDbContext _db;

    public ApplicationRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Application>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default)
    {
        var studentIdParam = new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId };
        const string sql = @"
            SELECT a.* 
            FROM dbo.Applications a 
            WHERE a.StudentId = @studentId 
            ORDER BY a.SubmittedAt DESC";

        return await _db.Applications
            .FromSqlRaw(sql, studentIdParam)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<Application?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id };
        const string sql = "SELECT a.* FROM dbo.Applications a WHERE a.Id = @id";

        return await _db.Applications
            .FromSqlRaw(sql, idParam)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }
}
