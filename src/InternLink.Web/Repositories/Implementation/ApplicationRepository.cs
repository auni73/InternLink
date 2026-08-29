using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

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

    public async Task<bool> HasAppliedAsync(Guid jobId, Guid studentId, CancellationToken ct = default)
    {
        var jobIdParam = new SqlParameter("@jobId", SqlDbType.UniqueIdentifier) { Value = jobId };
        var studentIdParam = new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId };

        const string sql = @"
            SELECT COUNT(*) AS Value 
            FROM dbo.Applications 
            WHERE JobId = @jobId AND StudentId = @studentId";

        var count = await _db.Database
            .SqlQueryRaw<int>(sql, jobIdParam, studentIdParam)
            .FirstOrDefaultAsync(ct);

        return count > 0;
    }

    public async Task<Application> ApplyAsync(
        Guid jobId, 
        Guid studentId, 
        Guid resumeId, 
        string? coverLetter, 
        CancellationToken ct = default)
    {
        var application = new Application
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            StudentId = studentId,
            AttachedResumeId = resumeId,
            CoverLetterText = coverLetter,
            ApplicationStatus = Models.Enums.ApplicationStatus.Applied,
            SubmittedAt = DateTimeOffset.UtcNow
        };

        var idParam = new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = application.Id };
        var jobIdParam = new SqlParameter("@jobId", SqlDbType.UniqueIdentifier) { Value = application.JobId };
        var studentIdParam = new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = application.StudentId };
        var resumeIdParam = new SqlParameter("@resumeId", SqlDbType.UniqueIdentifier) { Value = application.AttachedResumeId };
        var coverLetterParam = new SqlParameter("@coverLetter", SqlDbType.NVarChar, -1) 
        { 
            Value = string.IsNullOrWhiteSpace(application.CoverLetterText) ? DBNull.Value : application.CoverLetterText 
        };
        var statusParam = new SqlParameter("@status", SqlDbType.TinyInt) { Value = (byte)application.ApplicationStatus };
        var submittedAtParam = new SqlParameter("@submittedAt", SqlDbType.DateTimeOffset) { Value = application.SubmittedAt };

        const string sql = @"
            INSERT INTO dbo.Applications (Id, JobId, StudentId, AttachedResumeId, CoverLetterText, ApplicationStatus, SubmittedAt)
            VALUES (@id, @jobId, @studentId, @resumeId, @coverLetter, @status, @submittedAt)";

        await _db.Database.ExecuteSqlRawAsync(
            sql, 
            new object[] { idParam, jobIdParam, studentIdParam, resumeIdParam, coverLetterParam, statusParam, submittedAtParam }, 
            ct);

        return application;
    }

    public async Task<IReadOnlyList<StudentApplicationItemViewModel>> GetStudentApplicationsWithDetailsAsync(
        Guid studentId, 
        CancellationToken ct = default)
    {
        var studentIdParam = new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId };

        const string sql = @"
            SELECT 
                a.Id AS ApplicationId,
                a.JobId,
                j.Title AS JobTitle,
                c.CompanyName,
                j.LocationType,
                a.SubmittedAt,
                a.ApplicationStatus AS Status,
                a.AttachedResumeId,
                r.DocumentPath AS AttachedResumeName,
                a.CoverLetterText,
                i.Id AS InterviewId,
                i.ScheduledDateTime AS InterviewDateTime,
                i.ContextMeetingLink AS MeetingLink,
                i.StatusIndicator AS InterviewStatus
            FROM dbo.Applications a
            INNER JOIN dbo.Jobs j ON a.JobId = j.Id
            INNER JOIN dbo.Companies c ON j.CompanyId = c.Id
            LEFT JOIN dbo.Resumes r ON a.AttachedResumeId = r.Id
            LEFT JOIN dbo.Interviews i ON a.Id = i.ApplicationId
            WHERE a.StudentId = @studentId
            ORDER BY a.SubmittedAt DESC";

        var rows = await _db.Database
            .SqlQueryRaw<StudentAppRowResult>(sql, studentIdParam)
            .ToListAsync(ct);

        return rows.Select(r => new StudentApplicationItemViewModel
        {
            ApplicationId = r.ApplicationId,
            JobId = r.JobId,
            JobTitle = r.JobTitle,
            CompanyName = r.CompanyName,
            LocationType = (Models.Enums.LocationType)r.LocationType,
            SubmittedAt = r.SubmittedAt,
            Status = (Models.Enums.ApplicationStatus)r.Status,
            AttachedResumeId = r.AttachedResumeId,
            AttachedResumeName = r.AttachedResumeName != null ? "Finalized PDF Resume" : null,
            CoverLetterText = r.CoverLetterText,
            InterviewId = r.InterviewId,
            InterviewDateTime = r.InterviewDateTime,
            MeetingLink = r.MeetingLink,
            InterviewStatus = r.InterviewStatus.HasValue ? (Models.Enums.InterviewStatus)r.InterviewStatus.Value : null
        }).ToList();
    }
}

public class StudentAppRowResult
{
    public Guid ApplicationId { get; set; }
    public Guid JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public byte LocationType { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public byte Status { get; set; }
    public Guid? AttachedResumeId { get; set; }
    public string? AttachedResumeName { get; set; }
    public string? CoverLetterText { get; set; }
    public Guid? InterviewId { get; set; }
    public DateTimeOffset? InterviewDateTime { get; set; }
    public string? MeetingLink { get; set; }
    public byte? InterviewStatus { get; set; }
}
