using Microsoft.Extensions.Logging.Abstractions;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.AI;
using InternLink.Web.Services.Resume;
using InternLink.Web.ViewModels;
using Xunit;

namespace InternLink.Tests;

public class ResumeAnalysisServiceTests
{
    private static readonly Guid ResumeId = Guid.NewGuid();
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid StudentUserId = Guid.NewGuid();
    private static readonly Guid TargetJobId = Guid.NewGuid();

    private const string ValidScoreJson = """
    {
      "atsScore": 72,
      "grammarIssues": ["'Managed team' should specify team size."],
      "structureCritique": "Solid ordering, but the summary buries the strongest project.",
      "missingKeywords": ["ASP.NET Core", "unit testing"]
    }
    """;

    [Fact]
    public async Task ParsesAValidScoreResponse()
    {
        var harness = new Harness();
        harness.Gemini.Responses.Enqueue(ValidScoreJson);

        var result = await harness.Service.GetAtsScoreAsync(ResumeId, StudentId, null);

        Assert.Equal(72, result.AtsScore);
        Assert.Single(result.GrammarIssues);
        Assert.Equal(2, result.MissingKeywords.Count);
        Assert.Contains("summary buries", result.StructureCritique);
        Assert.Equal(1, harness.Gemini.CallCount);
    }

    [Fact]
    public async Task StripsCodeFences_ModelsStillEmitThemInJsonMode()
    {
        var harness = new Harness();
        harness.Gemini.Responses.Enqueue("```json\n" + ValidScoreJson + "\n```");

        var result = await harness.Service.GetAtsScoreAsync(ResumeId, StudentId, null);

        Assert.Equal(72, result.AtsScore);
    }

    [Fact]
    public async Task RetriesOnce_WithAStricterInstruction_WhenJsonIsMalformed()
    {
        var harness = new Harness();
        harness.Gemini.Responses.Enqueue("Sure! Here is the analysis you asked for.");
        harness.Gemini.Responses.Enqueue(ValidScoreJson);

        var result = await harness.Service.GetAtsScoreAsync(ResumeId, StudentId, null);

        Assert.Equal(72, result.AtsScore);
        Assert.Equal(2, harness.Gemini.CallCount);
        Assert.DoesNotContain("ONLY the JSON object", harness.Gemini.SystemPrompts[0]);
        Assert.Contains("ONLY the JSON object", harness.Gemini.SystemPrompts[1]);
    }

    [Fact]
    public async Task ReturnsSentinel_WhenBothAttemptsAreMalformed()
    {
        var harness = new Harness();
        harness.Gemini.Responses.Enqueue("not json");
        harness.Gemini.Responses.Enqueue("still not json");

        var result = await harness.Service.GetAtsScoreAsync(ResumeId, StudentId, null);

        Assert.Equal(AtsScoreResult.UnavailableScore, result.AtsScore);
        Assert.Contains("temporarily unavailable", result.StructureCritique, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, harness.Gemini.CallCount);
    }

    [Fact]
    public async Task ReturnsSentinelWithoutRetrying_WhenTheGatewayIsDown()
    {
        var harness = new Harness();
        harness.Gemini.ThrowAiServiceException = true;

        var result = await harness.Service.GetAtsScoreAsync(ResumeId, StudentId, null);

        Assert.Equal(AtsScoreResult.UnavailableScore, result.AtsScore);
        // A down gateway will not recover within one retry, so only one call is spent.
        Assert.Equal(1, harness.Gemini.CallCount);
    }

    [Fact]
    public async Task ReturnsSentinel_AndSpendsNoTokens_WhenTheResumeIsNotTheCallers()
    {
        var harness = new Harness();
        harness.Resumes.Resume = null;

        var result = await harness.Service.GetAtsScoreAsync(ResumeId, StudentId, null);

        Assert.Equal(AtsScoreResult.UnavailableScore, result.AtsScore);
        Assert.Equal(0, harness.Gemini.CallCount);
    }

    [Theory]
    [InlineData(140, 100)]
    [InlineData(-40, 0)]
    public async Task ClampsScoreIntoRange(int returned, int expected)
    {
        var harness = new Harness();
        harness.Gemini.Responses.Enqueue($$"""{ "atsScore": {{returned}}, "structureCritique": "ok" }""");

        var result = await harness.Service.GetAtsScoreAsync(ResumeId, StudentId, null);

        Assert.Equal(expected, result.AtsScore);
    }

    [Fact]
    public async Task IncludesTheTargetPosting_WhenOneIsSelected()
    {
        var harness = new Harness();
        harness.Gemini.Responses.Enqueue(ValidScoreJson);

        await harness.Service.GetAtsScoreAsync(ResumeId, StudentId, TargetJobId);

        Assert.Contains("Target role: Junior .NET Developer Intern", harness.Gemini.UserPrompts[0]);
        Assert.Contains("Score against this posting", harness.Gemini.UserPrompts[0]);
    }

    [Fact]
    public async Task LogsAgainstTheCorrectLedgerFeatures()
    {
        var harness = new Harness();
        harness.Gemini.Responses.Enqueue(ValidScoreJson);
        harness.Gemini.Responses.Enqueue("""{ "suggestions": [ { "originalText": "a", "suggestedText": "b", "reason": "c" } ] }""");

        await harness.Service.GetAtsScoreAsync(ResumeId, StudentId, null);
        await harness.Service.GetImprovementSuggestionsAsync(ResumeId, StudentId, TargetJobId);

        Assert.Equal(IntegrationFeature.AtsScoring, harness.Gemini.Features[0]);
        Assert.Equal(IntegrationFeature.ResumeSuggestions, harness.Gemini.Features[1]);
        Assert.All(harness.Gemini.UserIds, id => Assert.Equal(StudentUserId, id));
    }

    [Fact]
    public async Task ReturnsSuggestionPairs_DroppingEmptyRewrites()
    {
        var harness = new Harness();
        harness.Gemini.Responses.Enqueue("""
        { "suggestions": [
            { "originalText": "Did some testing", "suggestedText": "Wrote 40 xUnit tests covering the payment flow", "reason": "Quantifies impact." },
            { "originalText": "Helped out", "suggestedText": "", "reason": "empty" }
        ] }
        """);

        var suggestions = await harness.Service.GetImprovementSuggestionsAsync(ResumeId, StudentId, TargetJobId);

        var only = Assert.Single(suggestions);
        Assert.Equal("Did some testing", only.OriginalText);
        Assert.Contains("40 xUnit tests", only.SuggestedText);
    }

    [Fact]
    public async Task ReturnsNoSuggestions_WhenTheTargetJobIsMissing()
    {
        var harness = new Harness();
        harness.Jobs.Job = null;

        var suggestions = await harness.Service.GetImprovementSuggestionsAsync(ResumeId, StudentId, TargetJobId);

        Assert.Empty(suggestions);
        Assert.Equal(0, harness.Gemini.CallCount);
    }

    // ------------------------------------------------------------------ helpers

    private sealed class Harness
    {
        public FakeResumeService Resumes { get; } = new();
        public FakeStudentRepositoryForAnalysis Students { get; } = new();
        public FakeJobRepositoryForAnalysis Jobs { get; } = new();
        public RecordingGeminiClient Gemini { get; } = new();

        private ResumeAnalysisService? _service;

        public ResumeAnalysisService Service => _service ??= new ResumeAnalysisService(
            Resumes,
            Students,
            Jobs,
            Gemini,
            NullLogger<ResumeAnalysisService>.Instance);
    }

    private sealed class FakeResumeService : IResumeService
    {
        public ResumeBuilderViewModel? Resume { get; set; } = new()
        {
            ResumeId = ResumeId,
            StudentId = StudentId,
            IsFinalized = true,
            Data = new ResumeDataDto
            {
                PersonalInfo = new PersonalInfoStepDto { FullName = "Tanvir Ahmed", Summary = "CSE undergraduate." },
                Experience = [new ExperienceEntryDto { Company = "Acme", Role = "Intern", Description = "Did some testing" }]
            }
        };

        public Task<ResumeBuilderViewModel?> GetResumeForEditAsync(Guid resumeId, Guid studentId, CancellationToken ct = default) =>
            Task.FromResult(Resume);

        public Task<IReadOnlyList<ResumeItemViewModel>> GetStudentResumesAsync(Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<InternLink.Web.Models.Resume> CreateDraftResumeAsync(Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateStepAsync(Guid resumeId, Guid studentId, string stepName, string stepJson, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(bool Success, string? DocumentPath, string? ErrorMessage)> FinalizeResumeAsync(Guid resumeId, Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(Stream? Stream, string FileName)> OpenDownloadStreamAsync(Guid resumeId, Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeStudentRepositoryForAnalysis : IStudentRepository
    {
        public Task<InternLink.Web.Models.Student?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<InternLink.Web.Models.Student?>(new InternLink.Web.Models.Student { Id = StudentId, UserId = StudentUserId });

        public Task<InternLink.Web.Models.Student?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateProfileAsync(InternLink.Web.Models.Student student, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SyncStudentSkillsAsync(Guid studentId, IEnumerable<(Guid SkillId, int ProficiencyLevel)> skills, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<StudentSkill>> GetStudentSkillsAsync(Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class RecordingGeminiClient : IGeminiClient
    {
        public Queue<string> Responses { get; } = new();
        public List<string> SystemPrompts { get; } = [];
        public List<string> UserPrompts { get; } = [];
        public List<IntegrationFeature> Features { get; } = [];
        public List<Guid> UserIds { get; } = [];
        public bool ThrowAiServiceException { get; set; }
        public int CallCount => SystemPrompts.Count;

        public Task<GeminiResponse> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            IntegrationFeature feature,
            Guid userId,
            bool jsonMode,
            CancellationToken ct = default)
        {
            SystemPrompts.Add(systemPrompt);
            UserPrompts.Add(userPrompt);
            Features.Add(feature);
            UserIds.Add(userId);

            if (ThrowAiServiceException)
            {
                throw new AiServiceException("busy");
            }

            var content = Responses.Count > 0 ? Responses.Dequeue() : "{}";
            return Task.FromResult(new GeminiResponse(content, 100, 50, 0.0002m));
        }
    }

    private sealed class FakeJobRepositoryForAnalysis : IJobRepository
    {
        public Job? Job { get; set; } = new()
        {
            Id = TargetJobId,
            Title = "Junior .NET Developer Intern",
            CoreDescription = "Build ASP.NET Core MVC applications.",
            SelectionCriteria = "Strong C# and SQL Server fundamentals."
        };

        public Task<Job?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Job);

        public Task<IReadOnlyList<Job>> GetApprovedOpenJobsAsync(LocationType? locationType, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> GetApprovedOpenJobsCountAsync(LocationType? locationType, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<JobListItemViewModel> Items, int TotalCount)> SearchApprovedOpenJobsAsync(JobSearchFilter filter, Guid? studentId, bool isFtsAvailable, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<JobDetailViewModel?> GetApprovedJobDetailAsync(Guid id, Guid? studentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CompanyJobListItemViewModel>> GetCompanyJobsAsync(Guid companyId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CompanyJobEditViewModel?> GetCompanyJobForEditAsync(Guid jobId, Guid companyId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Guid> CreateJobWithSkillsAsync(Guid companyId, CompanyJobEditViewModel model, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateJobWithSkillsAsync(Guid jobId, Guid companyId, CompanyJobEditViewModel model, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> CloseJobAsync(Guid jobId, Guid companyId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<InternLink.Web.Services.Vectors.JobVectorSource?> GetJobVectorSourceAsync(Guid jobId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetApprovedOpenJobIdsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetAllJobIdsByCompanyUserIdAsync(Guid companyUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetIndexableJobIdsByCompanyUserIdAsync(Guid companyUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RecommendationCandidate>> GetRecommendationCandidatesAsync(IReadOnlyList<Guid> jobIds, Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RecommendationCandidate>> GetSkillOverlapRankedJobsAsync(Guid studentId, int take, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
