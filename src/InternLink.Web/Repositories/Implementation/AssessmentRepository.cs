using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Implementation;

public class AssessmentRepository : IAssessmentRepository
{
    private readonly ApplicationDbContext _db;

    public AssessmentRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SkillAssessmentListItemViewModel>> GetStudentSkillAssessmentsAsync(
        Guid studentId, 
        CancellationToken ct = default)
    {
        var studentIdParam = new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId };

        const string sql = @"
            SELECT 
                s.Id AS SkillId,
                s.SkillName,
                s.DomainClassification,
                CAST(CASE WHEN MAX(a.AchievedScore) >= 70 THEN 1 ELSE 0 END AS bit) AS IsVerified,
                MAX(a.AchievedScore) AS BestScore,
                COUNT(a.Id) AS AttemptsCount,
                MAX(a.EarnedDate) AS LastAttemptDate
            FROM dbo.Skills s
            LEFT JOIN dbo.Assessments a ON s.Id = a.SkillId AND a.StudentId = @studentId
            GROUP BY s.Id, s.SkillName, s.DomainClassification
            ORDER BY s.DomainClassification ASC, s.SkillName ASC";

        var rows = await _db.Database
            .SqlQueryRaw<SkillAssessmentRowResult>(sql, studentIdParam)
            .ToListAsync(ct);

        return rows.Select(r => new SkillAssessmentListItemViewModel
        {
            SkillId = r.SkillId,
            SkillName = r.SkillName,
            DomainClassification = r.DomainClassification,
            IsVerified = r.IsVerified,
            BestScore = r.BestScore,
            AttemptsCount = r.AttemptsCount,
            LastAttemptDate = r.LastAttemptDate
        }).ToList();
    }

    public async Task<bool> IsSkillVerifiedAsync(Guid studentId, Guid skillId, CancellationToken ct = default)
    {
        var studentIdParam = new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId };
        var skillIdParam = new SqlParameter("@skillId", SqlDbType.UniqueIdentifier) { Value = skillId };

        const string sql = @"
            SELECT CAST(CASE WHEN EXISTS (
                SELECT 1 
                FROM dbo.Assessments 
                WHERE StudentId = @studentId 
                  AND SkillId = @skillId 
                  AND AchievedScore >= 70
            ) THEN 1 ELSE 0 END AS bit) AS Value";

        return await _db.Database
            .SqlQueryRaw<bool>(sql, studentIdParam, skillIdParam)
            .FirstOrDefaultAsync(ct);
    }

    public async Task RecordAssessmentResultAsync(
        Guid studentId, 
        Guid skillId, 
        int achievedScore, 
        CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = Guid.NewGuid() };
        var studentIdParam = new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId };
        var skillIdParam = new SqlParameter("@skillId", SqlDbType.UniqueIdentifier) { Value = skillId };
        var scoreParam = new SqlParameter("@score", SqlDbType.Int) { Value = achievedScore };

        const string sql = @"
            INSERT INTO dbo.Assessments (Id, StudentId, SkillId, AchievedScore, EarnedDate)
            VALUES (@id, @studentId, @skillId, @score, SYSDATETIMEOFFSET())";

        await _db.Database.ExecuteSqlRawAsync(sql, new object[] { idParam, studentIdParam, skillIdParam, scoreParam }, ct);
    }

    public async Task<IReadOnlyList<Guid>> GetVerifiedSkillIdsAsync(Guid studentId, CancellationToken ct = default)
    {
        var studentIdParam = new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId };

        const string sql = @"
            SELECT DISTINCT SkillId AS Value
            FROM dbo.Assessments 
            WHERE StudentId = @studentId AND AchievedScore >= 70";

        return await _db.Database
            .SqlQueryRaw<Guid>(sql, studentIdParam)
            .ToListAsync(ct);
    }
}

public class SkillAssessmentRowResult
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public byte DomainClassification { get; set; }
    public bool IsVerified { get; set; }
    public int? BestScore { get; set; }
    public int AttemptsCount { get; set; }
    public DateTimeOffset? LastAttemptDate { get; set; }
}
