using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.Skills;
using InternLink.Web.Services.Storage;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Implementation;

public class ApplicationRepository : IApplicationRepository
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _fileStorage;
    private readonly IStudentSkillService _studentSkillService;

    public ApplicationRepository(
        ApplicationDbContext db,
        IFileStorage fileStorage,
        IStudentSkillService studentSkillService)
    {
        _db = db;
        _fileStorage = fileStorage;
        _studentSkillService = studentSkillService;
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

    public async Task<bool> UpdateCoverLetterAsync(
        Guid jobId,
        Guid studentId,
        string coverLetterText,
        CancellationToken ct = default)
    {
        var parameters = new[]
        {
            new SqlParameter("@jobId", SqlDbType.UniqueIdentifier) { Value = jobId },
            new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId },
            new SqlParameter("@coverLetter", SqlDbType.NVarChar, -1)
            {
                Value = string.IsNullOrWhiteSpace(coverLetterText) ? DBNull.Value : coverLetterText
            }
        };

        // Scoped by StudentId as well as JobId so one student can never overwrite another's letter.
        const string sql = @"
            UPDATE dbo.Applications 
            SET CoverLetterText = @coverLetter 
            WHERE JobId = @jobId AND StudentId = @studentId";

        var rows = await _db.Database.ExecuteSqlRawAsync(sql, parameters, ct);
        return rows > 0;
    }

    public async Task<IReadOnlyList<StudentApplicationItemViewModel>> GetStudentApplicationsWithDetailsAsync(
        Guid studentId, 
        CancellationToken ct = default)
    {        var studentIdParam = new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) { Value = studentId };

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

    public async Task<IReadOnlyList<JobFilterOptionDto>> GetCompanyJobFilterOptionsAsync(Guid companyId, CancellationToken ct = default)
    {
        var companyIdParam = new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId };

        const string sql = @"
            SELECT j.Id AS JobId, j.Title
            FROM dbo.Jobs j
            WHERE j.CompanyId = @companyId
            ORDER BY j.CreatedAt DESC";

        var rows = await _db.Database
            .SqlQueryRaw<JobFilterOptionRowResult>(sql, companyIdParam)
            .ToListAsync(ct);

        return rows.Select(r => new JobFilterOptionDto
        {
            JobId = r.JobId,
            Title = r.Title
        }).ToList();
    }

    public async Task<IReadOnlyList<CompanyAtsApplicantItemViewModel>> GetCompanyAtsApplicationsAsync(
        Guid companyId, 
        Guid? jobId, 
        CancellationToken ct = default)
    {
        var companyIdParam = new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId };
        var jobIdParam = new SqlParameter("@jobId", SqlDbType.UniqueIdentifier)
        {
            Value = jobId.HasValue ? (object)jobId.Value : DBNull.Value
        };

        const string sql = @"
            SELECT 
                a.Id AS ApplicationId,
                a.JobId,
                j.Title AS JobTitle,
                s.Id AS StudentId,
                (s.FirstName + ' ' + s.LastName) AS StudentName,
                s.Department,
                s.CGPA,
                a.SubmittedAt,
                a.ApplicationStatus AS Status,
                a.AttachedResumeId,
                (SELECT COUNT(DISTINCT a2.SkillId) 
                 FROM dbo.Assessments a2 
                 WHERE a2.StudentId = s.Id AND a2.AchievedScore >= 70) AS VerifiedSkillCount,
                i.Id AS InterviewId,
                i.ScheduledDateTime AS InterviewDateTime,
                i.ContextMeetingLink AS MeetingLink,
                i.StatusIndicator AS InterviewStatus
            FROM dbo.Applications a
            INNER JOIN dbo.Jobs j ON a.JobId = j.Id
            INNER JOIN dbo.Students s ON a.StudentId = s.Id
            LEFT JOIN dbo.Interviews i ON a.Id = i.ApplicationId
            WHERE j.CompanyId = @companyId
              AND (@jobId IS NULL OR j.Id = @jobId)
            ORDER BY a.SubmittedAt DESC";

        var rows = await _db.Database
            .SqlQueryRaw<CompanyAtsApplicantRowResult>(sql, companyIdParam, jobIdParam)
            .ToListAsync(ct);

        return rows.Select(r => new CompanyAtsApplicantItemViewModel
        {
            ApplicationId = r.ApplicationId,
            JobId = r.JobId,
            JobTitle = r.JobTitle,
            StudentId = r.StudentId,
            StudentName = r.StudentName,
            Department = r.Department,
            CGPA = r.CGPA,
            SubmittedAt = r.SubmittedAt,
            Status = (ApplicationStatus)r.Status,
            AttachedResumeId = r.AttachedResumeId,
            VerifiedSkillCount = r.VerifiedSkillCount,
            InterviewId = r.InterviewId,
            InterviewDateTime = r.InterviewDateTime,
            MeetingLink = r.MeetingLink,
            InterviewStatus = r.InterviewStatus.HasValue ? (InterviewStatus)r.InterviewStatus.Value : null
        }).ToList();
    }

    public async Task<CompanyAtsApplicantDetailViewModel?> GetCompanyApplicationDetailAsync(
        Guid applicationId, 
        Guid companyId, 
        CancellationToken ct = default)
    {
        var appIdParam = new SqlParameter("@appId", SqlDbType.UniqueIdentifier) { Value = applicationId };
        var companyIdParam = new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId };

        const string sql = @"
            SELECT 
                a.Id AS ApplicationId,
                a.JobId,
                j.Title AS JobTitle,
                s.Id AS StudentId,
                (s.FirstName + ' ' + s.LastName) AS StudentName,
                s.Department,
                s.CGPA,
                a.SubmittedAt,
                a.ApplicationStatus AS Status,
                a.CoverLetterText,
                a.AttachedResumeId,
                i.Id AS InterviewId,
                i.ScheduledDateTime AS InterviewDateTime,
                i.ContextMeetingLink AS MeetingLink,
                i.StatusIndicator AS InterviewStatus
            FROM dbo.Applications a
            INNER JOIN dbo.Jobs j ON a.JobId = j.Id
            INNER JOIN dbo.Students s ON a.StudentId = s.Id
            LEFT JOIN dbo.Interviews i ON a.Id = i.ApplicationId
            WHERE a.Id = @appId AND j.CompanyId = @companyId";

        var row = await _db.Database
            .SqlQueryRaw<CompanyAtsDetailRowResult>(sql, appIdParam, companyIdParam)
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        // Fetch verified skills via shared service logic
        var verifiedSkills = await _studentSkillService.GetVerifiedSkillsForStudentAsync(row.StudentId, ct);

        return new CompanyAtsApplicantDetailViewModel
        {
            ApplicationId = row.ApplicationId,
            JobId = row.JobId,
            JobTitle = row.JobTitle,
            StudentId = row.StudentId,
            StudentName = row.StudentName,
            Department = row.Department,
            CGPA = row.CGPA,
            SubmittedAt = row.SubmittedAt,
            Status = (ApplicationStatus)row.Status,
            CoverLetterText = row.CoverLetterText,
            AttachedResumeId = row.AttachedResumeId,
            VerifiedSkills = verifiedSkills,
            InterviewId = row.InterviewId,
            InterviewDateTime = row.InterviewDateTime,
            MeetingLink = row.MeetingLink,
            InterviewStatus = row.InterviewStatus.HasValue ? (InterviewStatus)row.InterviewStatus.Value : null
        };
    }

    public async Task<(bool Success, string? ErrorMessage)> TransitionApplicationStatusAsync(
        Guid applicationId, 
        Guid companyId, 
        ApplicationStatus newStatus, 
        DateTimeOffset? scheduledDateTime, 
        string? contextMeetingLink, 
        CancellationToken ct = default)
    {
        var appIdParam = new SqlParameter("@appId", SqlDbType.UniqueIdentifier) { Value = applicationId };
        var companyIdParam = new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId };

        const string checkSql = @"
            SELECT a.ApplicationStatus AS Status
            FROM dbo.Applications a
            INNER JOIN dbo.Jobs j ON a.JobId = j.Id
            WHERE a.Id = @appId AND j.CompanyId = @companyId";

        var currentStatusRow = await _db.Database
            .SqlQueryRaw<StatusCheckRowResult>(checkSql, appIdParam, companyIdParam)
            .FirstOrDefaultAsync(ct);

        if (currentStatusRow is null)
        {
            return (false, "Application not found or you do not have permission to modify it.");
        }

        var currentStatus = (ApplicationStatus)currentStatusRow.Status;

        // Server-Side Transition Graph Enforcement:
        // Applied -> Screened -> Scheduled -> { Offered, Rejected }
        // Rejected reachable from any non-terminal state
        // Never backward, terminal states (Offered, Rejected) are final.
        if (currentStatus == ApplicationStatus.Offered || currentStatus == ApplicationStatus.Rejected)
        {
            return (false, "Invalid status transition. Cannot transition an application in a terminal state (Offered / Rejected).");
        }

        if (newStatus == currentStatus)
        {
            return (false, "Invalid status transition. Application is already in this status.");
        }

        if (newStatus == ApplicationStatus.Rejected)
        {
            // Allowed from Applied, Screened, Scheduled
        }
        else if (currentStatus == ApplicationStatus.Applied)
        {
            if (newStatus != ApplicationStatus.Screened)
            {
                return (false, "Invalid status transition. Candidates in Applied status must be Screened before advancing.");
            }
        }
        else if (currentStatus == ApplicationStatus.Screened)
        {
            if (newStatus != ApplicationStatus.Scheduled)
            {
                return (false, "Invalid status transition. Screened candidates must have an interview Scheduled before advancing.");
            }
        }
        else if (currentStatus == ApplicationStatus.Scheduled)
        {
            if (newStatus != ApplicationStatus.Offered)
            {
                return (false, "Invalid status transition. Scheduled candidates can only be transitioned to Offered or Rejected.");
            }
        }
        else
        {
            return (false, "Invalid status transition.");
        }

        // Specific validation for newStatus == Scheduled
        if (newStatus == ApplicationStatus.Scheduled)
        {
            if (!scheduledDateTime.HasValue || scheduledDateTime.Value <= DateTimeOffset.UtcNow)
            {
                return (false, "Scheduled date and time must be a future date and time.");
            }

            if (string.IsNullOrWhiteSpace(contextMeetingLink) || 
                !Uri.TryCreate(contextMeetingLink.Trim(), UriKind.Absolute, out var uri) || 
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return (false, "A valid meeting link URL (http:// or https://) is required.");
            }
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            const string updateStatusSql = @"
                UPDATE dbo.Applications
                SET ApplicationStatus = @newStatus
                WHERE Id = @appId";

            await _db.Database.ExecuteSqlRawAsync(
                updateStatusSql, 
                new object[] {
                    new SqlParameter("@appId", SqlDbType.UniqueIdentifier) { Value = applicationId },
                    new SqlParameter("@newStatus", SqlDbType.TinyInt) { Value = (byte)newStatus }
                }, 
                ct);

            if (newStatus == ApplicationStatus.Scheduled)
            {
                const string upsertInterviewSql = @"
                    IF EXISTS (SELECT 1 FROM dbo.Interviews WHERE ApplicationId = @appId)
                    BEGIN
                        UPDATE dbo.Interviews
                        SET ScheduledDateTime = @scheduledDateTime,
                            ContextMeetingLink = @meetingLink,
                            StatusIndicator = 0
                        WHERE ApplicationId = @appId;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO dbo.Interviews (Id, ApplicationId, ScheduledDateTime, ContextMeetingLink, StatusIndicator, CreatedAt)
                        VALUES (@interviewId, @appId, @scheduledDateTime, @meetingLink, 0, SYSDATETIMEOFFSET());
                    END";

                await _db.Database.ExecuteSqlRawAsync(
                    upsertInterviewSql,
                    new object[] {
                        new SqlParameter("@interviewId", SqlDbType.UniqueIdentifier) { Value = Guid.NewGuid() },
                        new SqlParameter("@appId", SqlDbType.UniqueIdentifier) { Value = applicationId },
                        new SqlParameter("@scheduledDateTime", SqlDbType.DateTimeOffset) { Value = scheduledDateTime!.Value },
                        new SqlParameter("@meetingLink", SqlDbType.NVarChar, 500) { Value = contextMeetingLink!.Trim() }
                    },
                    ct);
            }

            await transaction.CommitAsync(ct);
            return (true, null);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<(Stream? Stream, string FileName)> OpenAuthorizedResumeStreamForCompanyAsync(
        Guid applicationId, 
        Guid companyId, 
        CancellationToken ct = default)
    {
        var appIdParam = new SqlParameter("@appId", SqlDbType.UniqueIdentifier) { Value = applicationId };
        var companyIdParam = new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId };

        const string sql = @"
            SELECT 
                r.DocumentPath,
                s.FirstName,
                s.LastName
            FROM dbo.Applications a
            INNER JOIN dbo.Jobs j ON a.JobId = j.Id
            INNER JOIN dbo.Resumes r ON a.AttachedResumeId = r.Id
            INNER JOIN dbo.Students s ON a.StudentId = s.Id
            WHERE a.Id = @appId AND j.CompanyId = @companyId";

        var row = await _db.Database
            .SqlQueryRaw<AuthorizedResumeRowResult>(sql, appIdParam, companyIdParam)
            .FirstOrDefaultAsync(ct);

        if (row is null || string.IsNullOrWhiteSpace(row.DocumentPath))
        {
            return (null, string.Empty);
        }

        var stream = await _fileStorage.OpenReadAsync(row.DocumentPath, ct);
        if (stream is null)
        {
            return (null, string.Empty);
        }

        var safeName = $"{row.FirstName}_{row.LastName}".Replace(" ", "_").Trim('_');
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "Candidate";
        }

        var fileName = $"{safeName}_Resume.pdf";
        return (stream, fileName);
    }
}

// Row result helper POCOs for SQL mapping
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

public class JobFilterOptionRowResult
{
    public Guid JobId { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class CompanyAtsApplicantRowResult
{
    public Guid ApplicationId { get; set; }
    public Guid JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal CGPA { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public byte Status { get; set; }
    public Guid? AttachedResumeId { get; set; }
    public int VerifiedSkillCount { get; set; }
    public Guid? InterviewId { get; set; }
    public DateTimeOffset? InterviewDateTime { get; set; }
    public string? MeetingLink { get; set; }
    public byte? InterviewStatus { get; set; }
}

public class CompanyAtsDetailRowResult
{
    public Guid ApplicationId { get; set; }
    public Guid JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal CGPA { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public byte Status { get; set; }
    public string? CoverLetterText { get; set; }
    public Guid? AttachedResumeId { get; set; }
    public Guid? InterviewId { get; set; }
    public DateTimeOffset? InterviewDateTime { get; set; }
    public string? MeetingLink { get; set; }
    public byte? InterviewStatus { get; set; }
}

public class StatusCheckRowResult
{
    public byte Status { get; set; }
}

public class AuthorizedResumeRowResult
{
    public string DocumentPath { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
