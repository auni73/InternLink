using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;

namespace InternLink.Web.Repositories.Implementation;

public class SkillRepository : ISkillRepository
{
    private readonly ApplicationDbContext _db;

    public SkillRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Skill>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT s.* FROM dbo.Skills s ORDER BY s.DomainClassification ASC, s.SkillName ASC";

        return await _db.Skills
            .FromSqlRaw(sql)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<Skill?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id };
        const string sql = "SELECT s.* FROM dbo.Skills s WHERE s.Id = @id";

        return await _db.Skills
            .FromSqlRaw(sql, idParam)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Skill>> GetSkillsByStudentIdAsync(Guid studentId, CancellationToken ct = default)
    {
        var studentIdParam = new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId };
        const string sql = @"
            SELECT s.* 
            FROM dbo.Skills s
            INNER JOIN dbo.StudentSkills ss ON s.Id = ss.SkillId
            WHERE ss.StudentId = @studentId
            ORDER BY s.SkillName ASC";

        return await _db.Skills
            .FromSqlRaw(sql, studentIdParam)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
