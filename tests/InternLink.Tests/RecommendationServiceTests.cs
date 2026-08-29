using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.AI;
using InternLink.Web.Services.Recommendation;
using InternLink.Web.Services.Vectors;
using InternLink.Web.ViewModels;
using Xunit;

namespace InternLink.Tests;

public class RecommendationServiceTests
{
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid StudentUserId = Guid.NewGuid();
    private static readonly Guid StrongJobId = Guid.NewGuid();
    private static readonly Guid WeakJobId = Guid.NewGuid();

    [Fact]
    public async Task RanksByHybridScore_AndComputesMatchPercentage()
    {
        // Strong: cosine 0.80, 4/4 skills -> 0.7*0.80 + 0.3*1.00 = 0.86 -> 86
        // Weak:   cosine 0.90, 2/4 skills -> 0.7*0.90 + 0.3*0.50 = 0.78 -> 78
        // The weaker candidate has the better cosine, so ordering proves skill overlap is weighted in.
        var harness = new Harness()
            .WithHit(StrongJobId, 0.80f, matched: 4, required: 4)
            .WithHit(WeakJobId, 0.90f, matched: 2, required: 4);

        var result = await harness.Service.GetRecommendedJobsAsync(StudentId);

        Assert.False(result.Degraded);
        Assert.Equal(2, result.Jobs.Count);
        Assert.Equal(StrongJobId, result.Jobs[0].JobId);
        Assert.Equal(86, result.Jobs[0].MatchPercentage);
        Assert.Equal(78, result.Jobs[1].MatchPercentage);
    }

    [Fact]
    public async Task ExcludesJobsTheStudentAlreadyAppliedTo()
    {
        var harness = new Harness()
            .WithHit(StrongJobId, 0.9f, matched: 3, required: 3, hasApplied: true)
            .WithHit(WeakJobId, 0.5f, matched: 1, required: 4);

        var result = await harness.Service.GetRecommendedJobsAsync(StudentId);

        Assert.DoesNotContain(result.Jobs, j => j.JobId == StrongJobId);
        Assert.Single(result.Jobs);
    }

    [Fact]
    public async Task UsesGeneratedReasons_WhenIdsMatchWhatWasSent()
    {
        var harness = new Harness().WithHit(StrongJobId, 0.8f, 4, 4);
        harness.Gemini.ResponseContent =
            $$"""{ "reasons": [ { "jobId": "{{StrongJobId}}", "reason": "Your C# depth lines up with this backend role." } ] }""";

        var result = await harness.Service.GetRecommendedJobsAsync(StudentId);

        Assert.Equal("Your C# depth lines up with this backend role.", result.Jobs[0].Reason);
    }

    [Fact]
    public async Task DropsHallucinatedJobIds_AndFallsBackToTemplateReason()
    {
        var harness = new Harness().WithHit(StrongJobId, 0.8f, 4, 4);
        harness.Gemini.ResponseContent =
            $$"""{ "reasons": [ { "jobId": "{{Guid.NewGuid()}}", "reason": "Reason for a job that was never sent." } ] }""";

        var result = await harness.Service.GetRecommendedJobsAsync(StudentId);

        Assert.DoesNotContain("never sent", result.Jobs[0].Reason);
        Assert.Contains("Matches 4", result.Jobs[0].Reason);
    }

    [Fact]
    public async Task StillReturnsRecommendations_WhenReasonWriterIsDown()
    {
        var harness = new Harness().WithHit(StrongJobId, 0.8f, 4, 4);
        harness.Gemini.ThrowAiServiceException = true;

        var result = await harness.Service.GetRecommendedJobsAsync(StudentId);

        Assert.Single(result.Jobs);
        Assert.False(result.Degraded);
        Assert.Contains("Matches 4", result.Jobs[0].Reason);
    }

    [Fact]
    public async Task FallsBackToSkillOverlap_WhenSemanticSearchIsUnavailable()
    {
        var harness = new Harness();
        harness.Embedder.ThrowUnavailable = true;
        harness.Jobs.FallbackCandidates =
        [
            Candidate(StrongJobId, matched: 3, required: 4, topSkill: "C#")
        ];

        var result = await harness.Service.GetRecommendedJobsAsync(StudentId);

        Assert.True(result.Degraded);
        Assert.Single(result.Jobs);
        // 3/4 overlap with no cosine component.
        Assert.Equal(75, result.Jobs[0].MatchPercentage);
        Assert.Contains("C#", result.Jobs[0].Reason);
        Assert.Equal(0, harness.Gemini.CallCount);
    }

    [Fact]
    public async Task CachesPerStudent_SoARefreshCostsNoEmbeddingOrLlmCall()
    {
        var harness = new Harness().WithHit(StrongJobId, 0.8f, 4, 4);

        await harness.Service.GetRecommendedJobsAsync(StudentId);
        await harness.Service.GetRecommendedJobsAsync(StudentId);

        Assert.Equal(1, harness.Embedder.CallCount);
        Assert.Equal(1, harness.Gemini.CallCount);
    }

    [Fact]
    public async Task ChangingASkill_BustsTheCache()
    {
        var harness = new Harness().WithHit(StrongJobId, 0.8f, 4, 4);

        await harness.Service.GetRecommendedJobsAsync(StudentId);
        harness.Students.Skills = BuildSkills(proficiency: 5);
        await harness.Service.GetRecommendedJobsAsync(StudentId);

        Assert.Equal(2, harness.Embedder.CallCount);
    }

    [Fact]
    public async Task EmbedsTheStudentProfileAsAQuery_NotADocument()
    {
        var harness = new Harness().WithHit(StrongJobId, 0.8f, 4, 4);

        await harness.Service.GetRecommendedJobsAsync(StudentId);

        Assert.Equal(EmbeddingPurpose.Query, harness.Embedder.LastPurpose);
        Assert.Contains("Computer Science", harness.Embedder.LastText);
        Assert.Contains("C# (level 4/5)", harness.Embedder.LastText);
    }

    // ------------------------------------------------------------------ helpers

    private static RecommendationCandidate Candidate(
        Guid jobId,
        int matched,
        int required,
        bool hasApplied = false,
        string? topSkill = "C#") => new()
        {
            JobId = jobId,
            Title = "Junior .NET Developer Intern",
            CompanyName = "TechCorp",
            LocationType = LocationType.Hybrid,
            DeadLine = DateTimeOffset.UtcNow.AddDays(30),
            RequiredSkillCount = required,
            MatchedSkillCount = matched,
            TopMatchedSkillName = topSkill,
            HasApplied = hasApplied
        };

    private static List<StudentSkill> BuildSkills(int proficiency) =>
    [
        new()
        {
            StudentId = StudentId,
            SkillId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ProficiencyLevel = proficiency,
            Skill = new Skill { SkillName = "C#" }
        }
    ];

    private sealed class Harness
    {
        public FakeStudentRepository Students { get; } = new()
        {
            Student = new Student
            {
                Id = StudentId,
                UserId = StudentUserId,
                Department = "Computer Science and Engineering",
                Interests = "Backend development",
                Biography = "Motivated CSE undergraduate."
            },
            Skills = BuildSkills(proficiency: 4)
        };

        public FakeJobRepositoryForRecommendations Jobs { get; } = new();
        public FakeEmbeddingClient Embedder { get; } = new();
        public FakeVectorSearch VectorSearch { get; } = new();
        public FakeGeminiClient Gemini { get; } = new();

        private RecommendationService? _service;

        // One instance, so the memory cache persists across calls within a test.
        public RecommendationService Service => _service ??= new RecommendationService(
            Students,
            Jobs,
            Embedder,
            VectorSearch,
            Gemini,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<RecommendationService>.Instance);

        public Harness WithHit(Guid jobId, float score, int matched, int required, bool hasApplied = false)
        {
            VectorSearch.Hits.Add((jobId, score));
            Jobs.Candidates.Add(Candidate(jobId, matched, required, hasApplied));
            return this;
        }
    }

    private sealed class FakeStudentRepository : IStudentRepository
    {
        public Student? Student { get; set; }
        public List<StudentSkill> Skills { get; set; } = [];

        public Task<Student?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Student);
        public Task<IReadOnlyList<StudentSkill>> GetStudentSkillsAsync(Guid studentId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StudentSkill>>(Skills);

        public Task<Student?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateProfileAsync(Student student, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SyncStudentSkillsAsync(Guid studentId, IEnumerable<(Guid SkillId, int ProficiencyLevel)> skills, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeEmbeddingClient : IEmbeddingClient
    {
        public int CallCount { get; private set; }
        public string LastText { get; private set; } = string.Empty;
        public EmbeddingPurpose LastPurpose { get; private set; }
        public bool ThrowUnavailable { get; set; }

        public Task<float[]> EmbedAsync(string text, EmbeddingPurpose purpose, CancellationToken ct = default)
        {
            CallCount++;
            LastText = text;
            LastPurpose = purpose;

            if (ThrowUnavailable)
            {
                throw new SemanticSearchUnavailableException("offline");
            }

            return Task.FromResult(new float[768]);
        }
    }

    private sealed class FakeVectorSearch : IVectorSearch
    {
        public List<(Guid JobId, float Score)> Hits { get; } = [];

        public Task<IReadOnlyList<(Guid JobId, float Score)>> SearchJobsAsync(
            float[] queryVector,
            int topK,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<(Guid, float)>>(Hits);
    }

    private sealed class FakeGeminiClient : IGeminiClient
    {
        public int CallCount { get; private set; }
        public string ResponseContent { get; set; } = """{ "reasons": [] }""";
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

            if (ThrowAiServiceException)
            {
                throw new AiServiceException("busy");
            }

            return Task.FromResult(new GeminiResponse(ResponseContent, 10, 5, 0.0001m));
        }

        public Task<GeminiResponse> GenerateChatAsync(
            string systemPrompt,
            IReadOnlyList<ChatMessage> history,
            IntegrationFeature feature,
            Guid userId,
            CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeJobRepositoryForRecommendations : IJobRepository
    {
        public List<RecommendationCandidate> Candidates { get; } = [];
        public IReadOnlyList<RecommendationCandidate> FallbackCandidates { get; set; } = [];

        public Task<IReadOnlyList<RecommendationCandidate>> GetRecommendationCandidatesAsync(
            IReadOnlyList<Guid> jobIds,
            Guid studentId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RecommendationCandidate>>(
                Candidates.Where(c => jobIds.Contains(c.JobId)).ToList());

        public Task<IReadOnlyList<RecommendationCandidate>> GetSkillOverlapRankedJobsAsync(
            Guid studentId,
            int take,
            CancellationToken ct = default) => Task.FromResult(FallbackCandidates);

        public Task<IReadOnlyList<Job>> GetApprovedOpenJobsAsync(LocationType? locationType, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Job?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> GetApprovedOpenJobsCountAsync(LocationType? locationType, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<JobListItemViewModel> Items, int TotalCount)> SearchApprovedOpenJobsAsync(JobSearchFilter filter, Guid? studentId, bool isFtsAvailable, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<JobDetailViewModel?> GetApprovedJobDetailAsync(Guid id, Guid? studentId, CancellationToken ct = default) => throw new NotSupportedException();
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
}
