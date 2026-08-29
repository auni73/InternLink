using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;

namespace InternLink.Web.Repositories.Implementation;

public class MockInterviewRepository : IMockInterviewRepository
{
    private readonly ApplicationDbContext _db;

    public MockInterviewRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task CreateSessionAsync(MockInterviewSession session, CancellationToken ct = default)
    {
        var parameters = new object[]
        {
            new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = session.Id },
            new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = session.StudentId },
            new SqlParameter("@role", SqlDbType.NVarChar, 100) { Value = session.Role },
            new SqlParameter("@jobId", SqlDbType.UniqueIdentifier) { Value = (object?)session.JobId ?? DBNull.Value },
            new SqlParameter("@transcript", SqlDbType.NVarChar, -1) { Value = session.TranscriptJson },
            new SqlParameter("@createdAt", SqlDbType.DateTimeOffset) { Value = session.CreatedAt }
        };

        const string sql = @"
            INSERT INTO dbo.MockInterviewSessions (Id, StudentId, Role, JobId, TranscriptJson, Status, CreatedAt)
            VALUES (@id, @studentId, @role, @jobId, @transcript, 0, @createdAt)";

        await _db.Database.ExecuteSqlRawAsync(sql, parameters, ct);
    }

    public async Task<MockInterviewSession?> GetSessionAsync(Guid sessionId, Guid studentId, CancellationToken ct = default)
    {
        var parameters = new object[]
        {
            new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = sessionId },
            new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId }
        };

        const string sql = @"
            SELECT Id, StudentId, Role, JobId, TranscriptJson, Status, ReportJson, CreatedAt, CompletedAt
            FROM dbo.MockInterviewSessions
            WHERE Id = @id AND StudentId = @studentId";

        var row = await _db.Database
            .SqlQueryRaw<MockInterviewSessionRowResult>(sql, parameters)
            .FirstOrDefaultAsync(ct);

        return row is null ? null : Map(row);
    }

    public async Task<bool> UpdateTranscriptAsync(
        Guid sessionId,
        Guid studentId,
        string transcriptJson,
        CancellationToken ct = default)
    {
        var parameters = new object[]
        {
            new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = sessionId },
            new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId },
            new SqlParameter("@transcript", SqlDbType.NVarChar, -1) { Value = transcriptJson }
        };

        // Status = 0 guards against appending turns to an interview that already produced its report.
        const string sql = @"
            UPDATE dbo.MockInterviewSessions
            SET TranscriptJson = @transcript
            WHERE Id = @id AND StudentId = @studentId AND Status = 0";

        var affected = await _db.Database.ExecuteSqlRawAsync(sql, parameters, ct);
        return affected > 0;
    }

    public async Task<bool> CompleteSessionAsync(
        Guid sessionId,
        Guid studentId,
        string reportJson,
        CancellationToken ct = default)
    {
        var parameters = new object[]
        {
            new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = sessionId },
            new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId },
            new SqlParameter("@report", SqlDbType.NVarChar, -1) { Value = reportJson }
        };

        const string sql = @"
            UPDATE dbo.MockInterviewSessions
            SET ReportJson = @report, Status = 1, CompletedAt = SYSDATETIMEOFFSET()
            WHERE Id = @id AND StudentId = @studentId AND Status = 0";

        var affected = await _db.Database.ExecuteSqlRawAsync(sql, parameters, ct);
        return affected > 0;
    }

    public async Task<IReadOnlyList<MockInterviewSession>> GetStudentSessionsAsync(
        Guid studentId,
        int take,
        CancellationToken ct = default)
    {
        var parameters = new object[]
        {
            new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId },
            new SqlParameter("@take", SqlDbType.Int) { Value = Math.Clamp(take, 1, 50) }
        };

        const string sql = @"
            SELECT TOP (@take) Id, StudentId, Role, JobId, TranscriptJson, Status, ReportJson, CreatedAt, CompletedAt
            FROM dbo.MockInterviewSessions
            WHERE StudentId = @studentId
            ORDER BY CreatedAt DESC";

        var rows = await _db.Database
            .SqlQueryRaw<MockInterviewSessionRowResult>(sql, parameters)
            .ToListAsync(ct);

        return rows.Select(Map).ToList();
    }

    private static MockInterviewSession Map(MockInterviewSessionRowResult row) => new()
    {
        Id = row.Id,
        StudentId = row.StudentId,
        Role = row.Role,
        JobId = row.JobId,
        TranscriptJson = row.TranscriptJson,
        Status = (MockInterviewStatus)row.Status,
        ReportJson = row.ReportJson,
        CreatedAt = row.CreatedAt,
        CompletedAt = row.CompletedAt
    };
}

public class MockInterviewSessionRowResult
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string Role { get; set; } = string.Empty;
    public Guid? JobId { get; set; }
    public string TranscriptJson { get; set; } = "[]";
    public byte Status { get; set; }
    public string? ReportJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
