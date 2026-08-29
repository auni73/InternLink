using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.Vectors;
using InternLink.Web.ViewModels;

namespace InternLink.Tests;

// Shared repository/service stubs. Every member throws unless a test overrides it, so a test that
// touches an unexpected dependency fails loudly instead of silently returning a default.

public class StubJobRepository : IJobRepository
{
    public virtual Task<Job?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public virtual Task<JobDetailViewModel?> GetApprovedJobDetailAsync(Guid id, Guid? studentId, CancellationToken ct = default) => throw new NotSupportedException();
    public virtual Task<IReadOnlyList<RecommendationCandidate>> GetRecommendationCandidatesAsync(IReadOnlyList<Guid> jobIds, Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();
    public virtual Task<IReadOnlyList<RecommendationCandidate>> GetSkillOverlapRankedJobsAsync(Guid studentId, int take, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<Job>> GetApprovedOpenJobsAsync(LocationType? locationType, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<int> GetApprovedOpenJobsCountAsync(LocationType? locationType, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<(IReadOnlyList<JobListItemViewModel> Items, int TotalCount)> SearchApprovedOpenJobsAsync(JobSearchFilter filter, Guid? studentId, bool isFtsAvailable, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<CompanyJobListItemViewModel>> GetCompanyJobsAsync(Guid companyId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<CompanyJobEditViewModel?> GetCompanyJobForEditAsync(Guid jobId, Guid companyId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Guid> CreateJobWithSkillsAsync(Guid companyId, CompanyJobEditViewModel model, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> UpdateJobWithSkillsAsync(Guid jobId, Guid companyId, CompanyJobEditViewModel model, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> CloseJobAsync(Guid jobId, Guid companyId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<JobVectorSource?> GetJobVectorSourceAsync(Guid jobId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<Guid>> GetApprovedOpenJobIdsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<Guid>> GetAllJobIdsByCompanyUserIdAsync(Guid companyUserId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<Guid>> GetIndexableJobIdsByCompanyUserIdAsync(Guid companyUserId, CancellationToken ct = default) => throw new NotSupportedException();
}

public class StubStudentRepository : IStudentRepository
{
    public virtual Task<Student?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public virtual Task<IReadOnlyList<StudentSkill>> GetStudentSkillsAsync(Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<Student?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task UpdateProfileAsync(Student student, CancellationToken ct = default) => throw new NotSupportedException();
    public Task SyncStudentSkillsAsync(Guid studentId, IEnumerable<(Guid SkillId, int ProficiencyLevel)> skills, CancellationToken ct = default) => throw new NotSupportedException();
}

public class StubApplicationRepository : IApplicationRepository
{
    public virtual Task<bool> UpdateCoverLetterAsync(Guid jobId, Guid studentId, string coverLetterText, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<Application>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Application?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> HasAppliedAsync(Guid jobId, Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Application> ApplyAsync(Guid jobId, Guid studentId, Guid resumeId, string? coverLetter, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<StudentApplicationItemViewModel>> GetStudentApplicationsWithDetailsAsync(Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<JobFilterOptionDto>> GetCompanyJobFilterOptionsAsync(Guid companyId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<CompanyAtsApplicantItemViewModel>> GetCompanyAtsApplicationsAsync(Guid companyId, Guid? jobId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<CompanyAtsApplicantDetailViewModel?> GetCompanyApplicationDetailAsync(Guid applicationId, Guid companyId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<(bool Success, string? ErrorMessage)> TransitionApplicationStatusAsync(Guid applicationId, Guid companyId, ApplicationStatus newStatus, DateTimeOffset? scheduledDateTime, string? contextMeetingLink, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<(Stream? Stream, string FileName)> OpenAuthorizedResumeStreamForCompanyAsync(Guid applicationId, Guid companyId, CancellationToken ct = default) => throw new NotSupportedException();
}

public class StubResumeService : InternLink.Web.Services.Resume.IResumeService
{
    public virtual Task<IReadOnlyList<ResumeItemViewModel>> GetStudentResumesAsync(Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();
    public virtual Task<ResumeBuilderViewModel?> GetResumeForEditAsync(Guid resumeId, Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<InternLink.Web.Models.Resume> CreateDraftResumeAsync(Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> UpdateStepAsync(Guid resumeId, Guid studentId, string stepName, string stepJson, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<(bool Success, string? DocumentPath, string? ErrorMessage)> FinalizeResumeAsync(Guid resumeId, Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<(Stream? Stream, string FileName)> OpenDownloadStreamAsync(Guid resumeId, Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();
}

/// <summary>In-memory mock interview store, scoped by student exactly like the SQL repository.</summary>
public class FakeMockInterviewRepository : IMockInterviewRepository
{
    public List<MockInterviewSession> Sessions { get; } = [];
    public int TranscriptWrites { get; private set; }

    public Task CreateSessionAsync(MockInterviewSession session, CancellationToken ct = default)
    {
        Sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<MockInterviewSession?> GetSessionAsync(Guid sessionId, Guid studentId, CancellationToken ct = default) =>
        Task.FromResult(Sessions.FirstOrDefault(s => s.Id == sessionId && s.StudentId == studentId));

    public Task<bool> UpdateTranscriptAsync(Guid sessionId, Guid studentId, string transcriptJson, CancellationToken ct = default)
    {
        var session = Sessions.FirstOrDefault(s =>
            s.Id == sessionId && s.StudentId == studentId && s.Status == MockInterviewStatus.InProgress);

        if (session is null)
        {
            return Task.FromResult(false);
        }

        session.TranscriptJson = transcriptJson;
        TranscriptWrites++;
        return Task.FromResult(true);
    }

    public Task<bool> CompleteSessionAsync(Guid sessionId, Guid studentId, string reportJson, CancellationToken ct = default)
    {
        var session = Sessions.FirstOrDefault(s =>
            s.Id == sessionId && s.StudentId == studentId && s.Status == MockInterviewStatus.InProgress);

        if (session is null)
        {
            return Task.FromResult(false);
        }

        session.ReportJson = reportJson;
        session.Status = MockInterviewStatus.Completed;
        session.CompletedAt = DateTimeOffset.UtcNow;
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<MockInterviewSession>> GetStudentSessionsAsync(Guid studentId, int take, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MockInterviewSession>>(
            Sessions.Where(s => s.StudentId == studentId)
                .OrderByDescending(s => s.CreatedAt)
                .Take(take)
                .ToList());
}
