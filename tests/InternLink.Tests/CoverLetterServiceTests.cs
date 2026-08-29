using Microsoft.Extensions.Logging.Abstractions;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Services.AI;
using InternLink.Web.Services.CoverLetter;
using InternLink.Web.ViewModels;
using Xunit;

namespace InternLink.Tests;

public class CoverLetterServiceTests
{
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid StudentUserId = Guid.NewGuid();
    private static readonly Guid JobId = Guid.NewGuid();

    [Fact]
    public async Task GeneratesProseWithJsonModeOff()
    {
        var harness = new Harness();

        var text = await harness.Service.GenerateAsync(StudentId, JobId);

        Assert.Equal("Generated letter body.", text);
        Assert.False(harness.Gemini.LastJsonMode);
        Assert.Equal(IntegrationFeature.CoverLetter, harness.Gemini.LastFeature);
        Assert.Equal(StudentUserId, harness.Gemini.LastUserId);
    }

    [Fact]
    public async Task ForbidsSalutationAndSignOff_BecauseThePdfRendererAddsThem()
    {
        var harness = new Harness();

        await harness.Service.GenerateAsync(StudentId, JobId);

        Assert.Contains("Do not include a salutation", harness.Gemini.LastSystemPrompt);
        Assert.Contains("250 to 400 words", harness.Gemini.LastSystemPrompt);
    }

    [Fact]
    public async Task GroundsThePromptInTheJobAndTheStudent()
    {
        var harness = new Harness();

        await harness.Service.GenerateAsync(StudentId, JobId);

        var prompt = harness.Gemini.LastUserPrompt;
        Assert.Contains("TechCorp Innovations Ltd.", prompt);
        Assert.Contains("Junior .NET Developer Intern", prompt);
        Assert.Contains("Strong C# fundamentals", prompt);
        Assert.Contains("C#", prompt);
        Assert.Contains("Library System", prompt);
    }

    [Fact]
    public async Task ReturnsNull_WhenTheJobIsNotAvailable()
    {
        var harness = new Harness();
        harness.Jobs.Job = null;

        var text = await harness.Service.GenerateAsync(StudentId, JobId);

        Assert.Null(text);
        Assert.Equal(0, harness.Gemini.CallCount);
    }

    [Fact]
    public async Task ReturnsNull_WhenTheStudentIsMissing()
    {
        var harness = new Harness();
        harness.Students.Student = null;

        var text = await harness.Service.GenerateAsync(StudentId, JobId);

        Assert.Null(text);
        Assert.Equal(0, harness.Gemini.CallCount);
    }

    [Fact]
    public async Task PropagatesGatewayFailure_SoTheUiCanShowARetry()
    {
        var harness = new Harness();
        harness.Gemini.ThrowAiServiceException = true;

        await Assert.ThrowsAsync<AiServiceException>(() => harness.Service.GenerateAsync(StudentId, JobId));
    }

    [Fact]
    public async Task SavesTheEditedTextVerbatim_WhenAnApplicationExists()
    {
        var harness = new Harness();

        var saved = await harness.Service.SaveToApplicationAsync(StudentId, JobId, "My edited letter.");

        Assert.True(saved);
        Assert.Equal("My edited letter.", harness.Applications.LastSavedText);
        Assert.Equal(JobId, harness.Applications.LastJobId);
        Assert.Equal(StudentId, harness.Applications.LastStudentId);
    }

    [Fact]
    public async Task ReportsFailure_WhenTheStudentHasNotAppliedYet()
    {
        var harness = new Harness();
        harness.Applications.UpdateSucceeds = false;

        var saved = await harness.Service.SaveToApplicationAsync(StudentId, JobId, "My edited letter.");

        Assert.False(saved);
    }

    [Fact]
    public async Task GeneratingDoesNotWriteAnything()
    {
        var harness = new Harness();

        await harness.Service.GenerateAsync(StudentId, JobId);

        // Nothing may reach the application row without an explicit save.
        Assert.Null(harness.Applications.LastSavedText);
    }

    // ------------------------------------------------------------------ helpers

    private sealed class Harness
    {
        public FakeStudents Students { get; } = new();
        public FakeJobs Jobs { get; } = new();
        public FakeResumes Resumes { get; } = new();
        public FakeApplications Applications { get; } = new();
        public RecordingGemini Gemini { get; } = new();

        private CoverLetterService? _service;

        public CoverLetterService Service => _service ??= new CoverLetterService(
            Students,
            Jobs,
            Resumes,
            Applications,
            Gemini,
            NullLogger<CoverLetterService>.Instance);
    }

    private sealed class FakeStudents : StubStudentRepository
    {
        public Student? Student { get; set; } = new()
        {
            Id = StudentId,
            UserId = StudentUserId,
            FirstName = "Tanvir",
            LastName = "Ahmed",
            Department = "Computer Science and Engineering",
            Interests = "Backend development",
            Biography = "Motivated CSE undergraduate."
        };

        public override Task<Student?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Student);

        public override Task<IReadOnlyList<StudentSkill>> GetStudentSkillsAsync(Guid studentId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StudentSkill>>(
            [
                new StudentSkill { SkillId = Guid.NewGuid(), ProficiencyLevel = 4, Skill = new Skill { SkillName = "C#" } }
            ]);
    }

    private sealed class FakeJobs : StubJobRepository
    {
        public JobDetailViewModel? Job { get; set; } = new()
        {
            Id = JobId,
            Title = "Junior .NET Developer Intern",
            CompanyName = "TechCorp Innovations Ltd.",
            CoreDescription = "Build ASP.NET Core MVC applications.",
            SelectionCriteria = "Strong C# fundamentals."
        };

        public override Task<JobDetailViewModel?> GetApprovedJobDetailAsync(Guid id, Guid? studentId, CancellationToken ct = default) =>
            Task.FromResult(Job);
    }

    private sealed class FakeResumes : StubResumeService
    {
        private static readonly Guid ResumeId = Guid.NewGuid();

        public override Task<IReadOnlyList<ResumeItemViewModel>> GetStudentResumesAsync(Guid studentId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ResumeItemViewModel>>(
            [
                new ResumeItemViewModel { Id = ResumeId, StudentId = studentId, DocumentPath = "x.pdf", LastModified = DateTimeOffset.UtcNow }
            ]);

        public override Task<ResumeBuilderViewModel?> GetResumeForEditAsync(Guid resumeId, Guid studentId, CancellationToken ct = default) =>
            Task.FromResult<ResumeBuilderViewModel?>(new ResumeBuilderViewModel
            {
                ResumeId = resumeId,
                StudentId = studentId,
                IsFinalized = true,
                Data = new ResumeDataDto
                {
                    PersonalInfo = new PersonalInfoStepDto { FullName = "Tanvir Ahmed", Summary = "CSE undergraduate." },
                    Projects = [new ProjectEntryDto { Title = "Library System", TechStack = "C#, SQL Server", Description = "Manages books." }]
                }
            });
    }

    private sealed class FakeApplications : StubApplicationRepository
    {
        public bool UpdateSucceeds { get; set; } = true;
        public string? LastSavedText { get; private set; }
        public Guid LastJobId { get; private set; }
        public Guid LastStudentId { get; private set; }

        public override Task<bool> UpdateCoverLetterAsync(Guid jobId, Guid studentId, string coverLetterText, CancellationToken ct = default)
        {
            if (!UpdateSucceeds)
            {
                return Task.FromResult(false);
            }

            LastJobId = jobId;
            LastStudentId = studentId;
            LastSavedText = coverLetterText;
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingGemini : IGeminiClient
    {
        public int CallCount { get; private set; }
        public string LastSystemPrompt { get; private set; } = string.Empty;
        public string LastUserPrompt { get; private set; } = string.Empty;
        public bool LastJsonMode { get; private set; } = true;
        public IntegrationFeature LastFeature { get; private set; }
        public Guid LastUserId { get; private set; }
        public bool ThrowAiServiceException { get; set; }

        public Task<GeminiResponse> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            IntegrationFeature feature,
            Guid userId,
            bool jsonMode,
            CancellationToken ct = default)
        {
            CallCount++;
            LastSystemPrompt = systemPrompt;
            LastUserPrompt = userPrompt;
            LastJsonMode = jsonMode;
            LastFeature = feature;
            LastUserId = userId;

            if (ThrowAiServiceException)
            {
                throw new AiServiceException("busy");
            }

            return Task.FromResult(new GeminiResponse("Generated letter body.", 200, 320, 0.0013m));
        }
    }
}
