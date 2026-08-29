using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Implementation;

public class CounselorRepository : ICounselorRepository
{
    private readonly ApplicationDbContext _db;

    public CounselorRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<CounselorStudentDirectoryItemViewModel> Items, int TotalCount)> GetStudentDirectoryAsync(
        string? search, 
        int page, 
        int pageSize, 
        CancellationToken ct = default)
    {
        var offset = Math.Max(0, (page - 1) * pageSize);
        var searchPattern = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";

        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        // 1. Total Count Query
        const string countSql = @"
            SELECT COUNT(*) 
            FROM dbo.Students s
            INNER JOIN dbo.AspNetUsers u ON s.UserId = u.Id
            WHERE (@search IS NULL OR 
                   s.FirstName LIKE @search OR 
                   s.LastName LIKE @search OR 
                   s.Department LIKE @search OR 
                   s.InstitutionalId LIKE @search OR 
                   u.Email LIKE @search);";

        int totalCount;
        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = countSql;
            var searchParam = countCmd.CreateParameter();
            searchParam.ParameterName = "@search";
            searchParam.Value = (object?)searchPattern ?? DBNull.Value;
            countCmd.Parameters.Add(searchParam);

            var countResult = await countCmd.ExecuteScalarAsync(ct);
            totalCount = Convert.ToInt32(countResult);
        }

        if (totalCount == 0)
        {
            return (Array.Empty<CounselorStudentDirectoryItemViewModel>(), 0);
        }

        // 2. Directory Query
        // Single hand-written query with COUNT subqueries: prevents N+1 queries when loading the student directory for every student in the system.
        const string directorySql = @"
            SELECT s.Id AS StudentId,
                   s.UserId,
                   s.FirstName,
                   s.LastName,
                   s.CGPA,
                   s.Department,
                   s.InstitutionalId,
                   u.Email,
                   (SELECT COUNT(*) FROM dbo.Resumes r WHERE r.StudentId = s.Id) AS ResumeCount,
                   (SELECT COUNT(*) FROM dbo.Applications a WHERE a.StudentId = s.Id) AS ApplicationCount
            FROM dbo.Students s
            INNER JOIN dbo.AspNetUsers u ON s.UserId = u.Id
            WHERE (@search IS NULL OR 
                   s.FirstName LIKE @search OR 
                   s.LastName LIKE @search OR 
                   s.Department LIKE @search OR 
                   s.InstitutionalId LIKE @search OR 
                   u.Email LIKE @search)
            ORDER BY s.LastName ASC, s.FirstName ASC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

        var items = new List<CounselorStudentDirectoryItemViewModel>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = directorySql;

            var searchParam = cmd.CreateParameter();
            searchParam.ParameterName = "@search";
            searchParam.Value = (object?)searchPattern ?? DBNull.Value;
            cmd.Parameters.Add(searchParam);

            var offsetParam = cmd.CreateParameter();
            offsetParam.ParameterName = "@offset";
            offsetParam.Value = offset;
            cmd.Parameters.Add(offsetParam);

            var pageSizeParam = cmd.CreateParameter();
            pageSizeParam.ParameterName = "@pageSize";
            pageSizeParam.Value = pageSize;
            cmd.Parameters.Add(pageSizeParam);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(new CounselorStudentDirectoryItemViewModel
                {
                    StudentId = reader.GetGuid(reader.GetOrdinal("StudentId")),
                    UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
                    FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                    LastName = reader.GetString(reader.GetOrdinal("LastName")),
                    CGPA = reader.GetDecimal(reader.GetOrdinal("CGPA")),
                    Department = reader.GetString(reader.GetOrdinal("Department")),
                    InstitutionalId = reader.GetString(reader.GetOrdinal("InstitutionalId")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    ResumeCount = reader.GetInt32(reader.GetOrdinal("ResumeCount")),
                    ApplicationCount = reader.GetInt32(reader.GetOrdinal("ApplicationCount"))
                });
            }
        }

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<CounselorFeedbackItemViewModel>> GetCounselorFeedbacksByStudentIdAsync(
        Guid studentId, 
        CancellationToken ct = default)
    {
        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        const string sql = @"
            SELECT cf.Id,
                   cf.StudentId,
                   cf.CounselorUserId,
                   cf.NarrativeMarkdown,
                   cf.MeetingDate,
                   COALESCE(u.Email, N'Counselor') AS CounselorEmail,
                   COALESCE(u.UserName, N'Counselor') AS CounselorName
            FROM dbo.CounselorFeedback cf
            INNER JOIN dbo.AspNetUsers u ON cf.CounselorUserId = u.Id
            WHERE cf.StudentId = @studentId
            ORDER BY cf.MeetingDate DESC, cf.Id DESC;";

        var list = new List<CounselorFeedbackItemViewModel>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var studentIdParam = cmd.CreateParameter();
        studentIdParam.ParameterName = "@studentId";
        studentIdParam.Value = studentId;
        cmd.Parameters.Add(studentIdParam);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new CounselorFeedbackItemViewModel
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                StudentId = reader.GetGuid(reader.GetOrdinal("StudentId")),
                CounselorUserId = reader.GetGuid(reader.GetOrdinal("CounselorUserId")),
                NarrativeMarkdown = reader.GetString(reader.GetOrdinal("NarrativeMarkdown")),
                MeetingDate = (DateTimeOffset)reader.GetValue(reader.GetOrdinal("MeetingDate")),
                CounselorEmail = reader.GetString(reader.GetOrdinal("CounselorEmail")),
                CounselorName = reader.GetString(reader.GetOrdinal("CounselorName"))
            });
        }

        return list;
    }

    public async Task<IReadOnlyList<CounselorFeedbackItemViewModel>> GetAdvisingNotesForStudentUserAsync(
        Guid studentUserId, 
        CancellationToken ct = default)
    {
        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        // Student-facing query: scoped strictly to the student's logged in UserId
        const string sql = @"
            SELECT cf.Id,
                   cf.StudentId,
                   cf.CounselorUserId,
                   cf.NarrativeMarkdown,
                   cf.MeetingDate,
                   COALESCE(u.Email, N'Counselor') AS CounselorEmail,
                   COALESCE(u.UserName, N'Counselor') AS CounselorName
            FROM dbo.CounselorFeedback cf
            INNER JOIN dbo.AspNetUsers u ON cf.CounselorUserId = u.Id
            INNER JOIN dbo.Students s ON cf.StudentId = s.Id
            WHERE s.UserId = @studentUserId
            ORDER BY cf.MeetingDate DESC, cf.Id DESC;";

        var list = new List<CounselorFeedbackItemViewModel>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var userParam = cmd.CreateParameter();
        userParam.ParameterName = "@studentUserId";
        userParam.Value = studentUserId;
        cmd.Parameters.Add(userParam);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new CounselorFeedbackItemViewModel
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                StudentId = reader.GetGuid(reader.GetOrdinal("StudentId")),
                CounselorUserId = reader.GetGuid(reader.GetOrdinal("CounselorUserId")),
                NarrativeMarkdown = reader.GetString(reader.GetOrdinal("NarrativeMarkdown")),
                MeetingDate = (DateTimeOffset)reader.GetValue(reader.GetOrdinal("MeetingDate")),
                CounselorEmail = reader.GetString(reader.GetOrdinal("CounselorEmail")),
                CounselorName = reader.GetString(reader.GetOrdinal("CounselorName"))
            });
        }

        return list;
    }

    public async Task<Guid> AddCounselorFeedbackAsync(
        Guid studentId, 
        Guid counselorUserId, 
        string narrativeMarkdown, 
        DateTimeOffset meetingDate, 
        CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        const string sql = @"
            INSERT INTO dbo.CounselorFeedback (Id, StudentId, CounselorUserId, NarrativeMarkdown, MeetingDate)
            VALUES (@id, @studentId, @counselorUserId, @narrativeMarkdown, @meetingDate);";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var idParam = cmd.CreateParameter();
        idParam.ParameterName = "@id";
        idParam.Value = id;
        cmd.Parameters.Add(idParam);

        var studentParam = cmd.CreateParameter();
        studentParam.ParameterName = "@studentId";
        studentParam.Value = studentId;
        cmd.Parameters.Add(studentParam);

        var counselorParam = cmd.CreateParameter();
        counselorParam.ParameterName = "@counselorUserId";
        counselorParam.Value = counselorUserId;
        cmd.Parameters.Add(counselorParam);

        var markdownParam = cmd.CreateParameter();
        markdownParam.ParameterName = "@narrativeMarkdown";
        markdownParam.Value = narrativeMarkdown;
        cmd.Parameters.Add(markdownParam);

        var dateParam = cmd.CreateParameter();
        dateParam.ParameterName = "@meetingDate";
        dateParam.Value = meetingDate;
        cmd.Parameters.Add(dateParam);

        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }
}
