using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;

namespace InternLink.Web.Repositories.Implementation;

public class StudentRepository : IStudentRepository
{
    private readonly ApplicationDbContext _db;

    public StudentRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Student?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id };
        const string sql = "SELECT s.* FROM dbo.Students s WHERE s.Id = @id";

        return await _db.Students
            .FromSqlRaw(sql, idParam)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Student?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var userIdParam = new SqlParameter("@userId", SqlDbType.UniqueIdentifier) { Value = userId };
        const string sql = "SELECT s.* FROM dbo.Students s WHERE s.UserId = @userId";

        return await _db.Students
            .FromSqlRaw(sql, userIdParam)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }
}
