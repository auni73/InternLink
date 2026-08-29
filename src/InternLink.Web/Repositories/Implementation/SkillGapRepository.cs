using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Implementation;

public class SkillGapRepository : ISkillGapRepository
{
    private const int VerifiedScoreThreshold = 70;

    private readonly ApplicationDbContext _db;

    public SkillGapRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<JobRequiredSkillRow>> GetJobRequiredSkillsAsync(
        Guid jobId,
        CancellationToken ct = default)
    {
        var jobIdParam = new SqlParameter("@jobId", SqlDbType.UniqueIdentifier) { Value = jobId };

        // RequiredImportanceWeight is INT in the schema; CAST keeps it aligned with the byte row type.
        const string sql = @"
            SELECT s.Id AS SkillId,
                   s.SkillName,
                   CAST(s.DomainClassification AS tinyint) AS Domain,
                   CAST(js.RequiredImportanceWeight AS tinyint) AS Weight
            FROM dbo.JobSkills js
            INNER JOIN dbo.Skills s ON s.Id = js.SkillId
            WHERE js.JobId = @jobId
            ORDER BY js.RequiredImportanceWeight DESC, s.SkillName ASC";

        return await _db.Database
            .SqlQueryRaw<JobRequiredSkillRow>(sql, jobIdParam)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StudentHeldSkillRow>> GetStudentHeldSkillsAsync(
        Guid studentId,
        CancellationToken ct = default)
    {
        var parameters = new object[]
        {
            new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId },
            new SqlParameter("@threshold", SqlDbType.Int) { Value = VerifiedScoreThreshold }
        };

        const string sql = @"
            SELECT s.Id AS SkillId,
                   s.SkillName,
                   CAST(s.DomainClassification AS tinyint) AS Domain,
                   ss.ProficiencyLevel,
                   CAST(CASE WHEN EXISTS (
                       SELECT 1 FROM dbo.Assessments a
                       WHERE a.StudentId = ss.StudentId
                         AND a.SkillId = ss.SkillId
                         AND a.AchievedScore >= @threshold
                   ) THEN 1 ELSE 0 END AS bit) AS IsVerified
            FROM dbo.StudentSkills ss
            INNER JOIN dbo.Skills s ON s.Id = ss.SkillId
            WHERE ss.StudentId = @studentId
            ORDER BY s.SkillName ASC";

        return await _db.Database
            .SqlQueryRaw<StudentHeldSkillRow>(sql, parameters)
            .ToListAsync(ct);
    }

    public async Task<ApplicationSkillGapScope?> GetApplicationScopeAsync(
        Guid applicationId,
        Guid companyId,
        CancellationToken ct = default)
    {
        var parameters = new object[]
        {
            new SqlParameter("@applicationId", SqlDbType.UniqueIdentifier) { Value = applicationId },
            new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId }
        };

        const string sql = @"
            SELECT a.StudentId,
                   a.JobId,
                   st.FirstName + N' ' + st.LastName AS StudentName,
                   j.Title AS JobTitle
            FROM dbo.Applications a
            INNER JOIN dbo.Jobs j ON j.Id = a.JobId
            INNER JOIN dbo.Students st ON st.Id = a.StudentId
            WHERE a.Id = @applicationId AND j.CompanyId = @companyId";

        return await _db.Database
            .SqlQueryRaw<ApplicationSkillGapScope>(sql, parameters)
            .FirstOrDefaultAsync(ct);
    }
}
