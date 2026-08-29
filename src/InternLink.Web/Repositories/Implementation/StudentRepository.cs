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

    public async Task UpdateProfileAsync(Student student, CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = student.Id };
        var firstNameParam = new SqlParameter("@firstName", SqlDbType.NVarChar, 100) { Value = student.FirstName };
        var lastNameParam = new SqlParameter("@lastName", SqlDbType.NVarChar, 100) { Value = student.LastName };
        var cgpaParam = new SqlParameter("@cgpa", SqlDbType.Decimal) { Value = student.CGPA, Precision = 3, Scale = 2 };
        var deptParam = new SqlParameter("@department", SqlDbType.NVarChar, 100) { Value = student.Department };
        var bioParam = new SqlParameter("@biography", SqlDbType.NVarChar, 2000) 
        { 
            Value = string.IsNullOrWhiteSpace(student.Biography) ? DBNull.Value : student.Biography 
        };
        var interestsParam = new SqlParameter("@interests", SqlDbType.NVarChar, 500) 
        { 
            Value = string.IsNullOrWhiteSpace(student.Interests) ? DBNull.Value : student.Interests 
        };

        // Note: InstitutionalId is strictly protected and never updated.
        const string sql = @"
            UPDATE dbo.Students 
            SET FirstName = @firstName,
                LastName = @lastName,
                CGPA = @cgpa,
                Department = @department,
                Biography = @biography,
                Interests = @interests
            WHERE Id = @id";

        await _db.Database.ExecuteSqlRawAsync(
            sql, 
            new object[] { idParam, firstNameParam, lastNameParam, cgpaParam, deptParam, bioParam, interestsParam }, 
            ct);
    }

    public async Task SyncStudentSkillsAsync(
        Guid studentId, 
        IEnumerable<(Guid SkillId, int ProficiencyLevel)> skills, 
        CancellationToken ct = default)
    {
        var skillsList = skills.ToList();
        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            var deleteStudentIdParam = new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId };
            const string deleteSql = "DELETE FROM dbo.StudentSkills WHERE StudentId = @studentId";
            await _db.Database.ExecuteSqlRawAsync(deleteSql, new object[] { deleteStudentIdParam }, ct);

            foreach (var (skillId, proficiency) in skillsList)
            {
                var sIdParam = new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId };
                var kIdParam = new SqlParameter("@skillId", SqlDbType.UniqueIdentifier) { Value = skillId };
                var profParam = new SqlParameter("@prof", SqlDbType.Int) { Value = Math.Clamp(proficiency, 1, 5) };

                const string insertSql = @"
                    INSERT INTO dbo.StudentSkills (StudentId, SkillId, ProficiencyLevel) 
                    VALUES (@studentId, @skillId, @prof)";

                await _db.Database.ExecuteSqlRawAsync(insertSql, new object[] { sIdParam, kIdParam, profParam }, ct);
            }

            await transaction.CommitAsync(ct);
        });
    }

    public async Task<IReadOnlyList<StudentSkill>> GetStudentSkillsAsync(Guid studentId, CancellationToken ct = default)
    {
        var studentIdParam = new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId };
        const string sql = @"
            SELECT ss.* 
            FROM dbo.StudentSkills ss 
            WHERE ss.StudentId = @studentId";

        return await _db.StudentSkills
            .FromSqlRaw(sql, studentIdParam)
            .Include(ss => ss.Skill)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
