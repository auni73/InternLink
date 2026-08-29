using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;

namespace InternLink.Web.Repositories.Implementation;

public class ResumeRepository : IResumeRepository
{
    private readonly ApplicationDbContext _db;

    public ResumeRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Resume?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id };
        const string sql = "SELECT r.* FROM dbo.Resumes r WHERE r.Id = @id";

        return await _db.Resumes
            .FromSqlRaw(sql, idParam)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Resume>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default)
    {
        var studentIdParam = new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId };
        const string sql = @"
            SELECT r.* 
            FROM dbo.Resumes r 
            WHERE r.StudentId = @studentId 
            ORDER BY r.LastModified DESC";

        return await _db.Resumes
            .FromSqlRaw(sql, studentIdParam)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<Resume> CreateAsync(Guid studentId, CancellationToken ct = default)
    {
        var resume = new Resume
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            DocumentPath = null,
            DynamicJsonData = "{}",
            LastModified = DateTimeOffset.UtcNow
        };

        var idParam = new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = resume.Id };
        var studentIdParam = new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = resume.StudentId };
        var jsonParam = new SqlParameter("@jsonData", SqlDbType.NVarChar, -1) { Value = resume.DynamicJsonData };
        var lastModifiedParam = new SqlParameter("@lastModified", SqlDbType.DateTimeOffset) { Value = resume.LastModified };

        const string sql = @"
            INSERT INTO dbo.Resumes (Id, StudentId, DocumentPath, DynamicJsonData, LastModified) 
            VALUES (@id, @studentId, NULL, @jsonData, @lastModified)";

        await _db.Database.ExecuteSqlRawAsync(sql, new object[] { idParam, studentIdParam, jsonParam, lastModifiedParam }, ct);

        return resume;
    }

    public async Task UpdateDynamicJsonDataAsync(
        Guid id, 
        string dynamicJsonData, 
        DateTimeOffset lastModified, 
        CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id };
        var jsonParam = new SqlParameter("@jsonData", SqlDbType.NVarChar, -1) { Value = dynamicJsonData };
        var lastModifiedParam = new SqlParameter("@lastModified", SqlDbType.DateTimeOffset) { Value = lastModified };

        const string sql = @"
            UPDATE dbo.Resumes 
            SET DynamicJsonData = @jsonData, 
                LastModified = @lastModified 
            WHERE Id = @id";

        await _db.Database.ExecuteSqlRawAsync(sql, new object[] { idParam, jsonParam, lastModifiedParam }, ct);
    }

    public async Task FinalizeAsync(
        Guid id, 
        string documentPath, 
        DateTimeOffset lastModified, 
        CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id };
        var docPathParam = new SqlParameter("@docPath", SqlDbType.NVarChar, 500) { Value = documentPath };
        var lastModifiedParam = new SqlParameter("@lastModified", SqlDbType.DateTimeOffset) { Value = lastModified };

        const string sql = @"
            UPDATE dbo.Resumes 
            SET DocumentPath = @docPath, 
                LastModified = @lastModified 
            WHERE Id = @id";

        await _db.Database.ExecuteSqlRawAsync(sql, new object[] { idParam, docPathParam, lastModifiedParam }, ct);
    }

    public async Task<int> GetFinalizedCountByStudentIdAsync(Guid studentId, CancellationToken ct = default)
    {
        var studentIdParam = new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId };
        const string sql = @"
            SELECT COUNT(*) AS Value 
            FROM dbo.Resumes 
            WHERE StudentId = @studentId 
              AND DocumentPath IS NOT NULL";

        return await _db.Database
            .SqlQueryRaw<int>(sql, studentIdParam)
            .FirstOrDefaultAsync(ct);
    }
}
