using Microsoft.Extensions.Logging.Abstractions;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Services.AI;
using InternLink.Web.Services.SkillGap;
using InternLink.Web.ViewModels;
using Xunit;

namespace InternLink.Tests;

public class SkillGapServiceTests
{
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid StudentUserId = Guid.NewGuid();
    private static readonly Guid JobId = Guid.NewGuid();

    private static readonly Guid CSharp = Guid.NewGuid();
    private static readonly Guid AspNet = Guid.NewGuid();
    private static readonly Guid SqlServer = Guid.NewGuid();
    private static readonly Guid Bootstrap = Guid.NewGuid();
    private static readonly Guid Docker = Guid.NewGuid();

    // ---- Deterministic half ----

    [Fact]
    public async Task ComputesHaveNeedMatchedAndGapExactly()
    {
        var harness = new Harness();
        harness.Repo.Required.AddRange([
            Required(CSharp, "C#", 5),
            Required(AspNet, "ASP.NET Core", 5),
            Required(SqlServer, "SQL Server", 4),
            Required(Bootstrap, "Bootstrap 5", 3)
        ]);
        harness.Repo.Held.AddRange([
            Held(CSharp, "C#", 4, verified: true),
            Held(SqlServer, "SQL Server", 3, verified: false),
            Held(Docker, "Docker", 2, verified: false)
        ]);
        harness.Gemini.ResponseContent = """{ "suggestions": [] }""";

        var result = await harness.Service.AnalyzeAsync(StudentId, JobId, SkillGapPerspective.Student);

        Assert.Equal(3, result.Have.Count);
        Assert.Equal(4, result.Needed.Count);
        Assert.Equal(["C#", "SQL Server"], result.Matched.Select(s => s.SkillName).OrderBy(n => n));
        Assert.Equal(["ASP.NET Core", "Bootstrap 5"], result.Gap.Select(s => s.SkillName).OrderBy(n => n));

        // Docker is held but not required: it belongs in Have and nowhere else.
        Assert.DoesNotContain(result.Needed, s => s.SkillName == "Docker");
        Assert.DoesNotContain(result.Gap, s => s.SkillName == "Docker");
    }

    [Fact]
    public async Task MatchesOnSkillIdNotName()
    {
        var harness = new Harness();
        harness.Repo.Required.Add(Required(CSharp, "C#", 5));
        // Same skill id, a different stored label. Identity must win over the string.
        harness.Repo.Held.Add(Held(CSharp, "C Sharp", 4, verified: false));
        harness.Gemini.ResponseContent = """{ "suggestions": [] }""";

        var result = await harness.Service.AnalyzeAsync(StudentId, JobId, SkillGapPerspective.Student);

        Assert.Single(result.Matched);
        Assert.Empty(result.Gap);
    }

    [Fact]
    public async Task FlagsWeightFourAndAboveAsMustHave()
    {
        var harness = new Harness();
        harness.Repo.Required.AddRange([
            Required(CSharp, "C#", 5),
            Required(SqlServer, "SQL Server", 4),
            Required(Bootstrap, "Bootstrap 5", 3)
        ]);
        harness.Gemini.ResponseContent = """{ "suggestions": [] }""";

        var result = await harness.Service.AnalyzeAsync(StudentId, JobId, SkillGapPerspective.Student);

        Assert.True(result.Needed.Single(s => s.SkillName == "C#").IsMustHave);
        Assert.True(result.Needed.Single(s => s.SkillName == "SQL Server").IsMustHave);
        Assert.False(result.Needed.Single(s => s.SkillName == "Bootstrap 5").IsMustHave);
        Assert.Equal(2, result.MustHaveGapCount);
    }

    [Fact]
    public async Task CarriesProficiencyAndVerificationOntoMatchedRequirements()
    {
        var harness = new Harness();
        harness.Repo.Required.Add(Required(CSharp, "C#", 5));
        harness.Repo.Held.Add(Held(CSharp, "C#", 4, verified: true));
        harness.Gemini.ResponseContent = """{ "suggestions": [] }""";

        var result = await harness.Service.AnalyzeAsync(StudentId, JobId, SkillGapPerspective.Student);

        var need = result.Needed.Single();
        Assert.Equal(4, need.ProficiencyLevel);
        Assert.True(need.IsVerified);
    }

    [Fact]
    public async Task ReportsFullMatchWhenTheJobListsNoSkills()
    {
        var harness = new Harness();

        var result = await harness.Service.AnalyzeAsync(StudentId, JobId, SkillGapPerspective.Student);

        Assert.Equal(100, result.MatchPercentage);
        Assert.Empty(result.Gap);
        Assert.Equal(0, harness.Gemini.CallCount);
    }

    // ---- AI half ----

    [Fact]
    public async Task SendsOnlyTheMissingSkillNamesToTheModel()
    {
        var harness = new Harness();
        harness.Repo.Required.AddRange([Required(CSharp, "C#", 5), Required(Docker, "Docker", 4)]);
        harness.Repo.Held.Add(Held(CSharp, "C#", 4, verified: true));
        harness.Gemini.ResponseContent = """{ "suggestions": [] }""";

        await harness.Service.AnalyzeAsync(StudentId, JobId, SkillGapPerspective.Student);

        var prompt = harness.Gemini.LastUserPrompt;
        Assert.Contains("Docker", prompt);
        // A skill the student already holds is not the model's business, and neither is anything else.
        Assert.DoesNotContain("C#", prompt);
        Assert.DoesNotContain("Tanvir", prompt);
        Assert.DoesNotContain("CGPA", prompt);
        Assert.True(harness.Gemini.LastJsonMode);
        Assert.Equal(IntegrationFeature.SkillGap, harness.Gemini.LastFeature);
        Assert.Equal(StudentUserId, harness.Gemini.LastUserId);
    }

    [Fact]
    public async Task AttachesSuggestionsToTheMatchingGapSkill()
    {
        var harness = new Harness();
        harness.Repo.Required.AddRange([Required(Docker, "Docker", 4), Required(Bootstrap, "Bootstrap 5", 3)]);
        harness.Gemini.ResponseContent = """
            { "suggestions": [
                { "skillName": "docker", "learningResources": ["freeCodeCamp's Docker course", "Docker's own Getting Started guide"] },
                { "skillName": "Bootstrap 5", "learningResources": ["The official Bootstrap 5 docs"] }
            ] }
            """;

        var result = await harness.Service.AnalyzeAsync(StudentId, JobId, SkillGapPerspective.Student);

        // Casing drifts in model output, so the join is case-insensitive.
        var docker = result.Gap.Single(s => s.SkillName == "Docker");
        Assert.Equal(2, docker.LearningResources.Count);
        Assert.Contains("freeCodeCamp's Docker course", docker.LearningResources);
        Assert.Single(result.Gap.Single(s => s.SkillName == "Bootstrap 5").LearningResources);
        Assert.True(result.SuggestionsAvailable);
    }

    [Fact]
    public async Task SkipsTheModelEntirelyWhenNothingIsMissing()
    {
        var harness = new Harness();
        harness.Repo.Required.Add(Required(CSharp, "C#", 5));
        harness.Repo.Held.Add(Held(CSharp, "C#", 4, verified: true));

        var result = await harness.Service.AnalyzeAsync(StudentId, JobId, SkillGapPerspective.Student);

        Assert.Equal(0, harness.Gemini.CallCount);
        Assert.True(result.SuggestionsAvailable);
    }

    [Fact]
    public async Task KeepsTheSetsWhenTheModelIsUnavailable()
    {
        var harness = new Harness();
        harness.Repo.Required.AddRange([Required(CSharp, "C#", 5), Required(Docker, "Docker", 4)]);
        harness.Repo.Held.Add(Held(CSharp, "C#", 4, verified: true));
        harness.Gemini.ThrowAiServiceException = true;

        var result = await harness.Service.AnalyzeAsync(StudentId, JobId, SkillGapPerspective.Student);

        // The deterministic half never hides behind the AI half.
        Assert.False(result.SuggestionsAvailable);
        Assert.Single(result.Matched);
        Assert.Single(result.Gap);
        Assert.Equal("Docker", result.Gap[0].SkillName);
        Assert.Empty(result.Gap[0].LearningResources);
    }

    [Fact]
    public async Task KeepsTheSetsWhenTheModelReturnsUnparseableJson()
    {
        var harness = new Harness();
        harness.Repo.Required.Add(Required(Docker, "Docker", 4));
        harness.Gemini.ResponseContent = "not json at all";

        var result = await harness.Service.AnalyzeAsync(StudentId, JobId, SkillGapPerspective.Student);

        Assert.False(result.SuggestionsAvailable);
        Assert.Single(result.Gap);
        // Retried once with the stricter instruction before giving up.
        Assert.Equal(2, harness.Gemini.CallCount);
    }

    [Fact]
    public async Task LeavesUnmatchedGapSkillsWithoutSuggestions()
    {
        var harness = new Harness();
        harness.Repo.Required.AddRange([Required(Docker, "Docker", 4), Required(Bootstrap, "Bootstrap 5", 3)]);
        harness.Gemini.ResponseContent = """
            { "suggestions": [{ "skillName": "Kubernetes", "learningResources": ["Something unrelated"] }] }
            """;

        var result = await harness.Service.AnalyzeAsync(StudentId, JobId, SkillGapPerspective.Student);

        // A hallucinated skill name must not leak onto a real gap entry.
        Assert.All(result.Gap, s => Assert.Empty(s.LearningResources));
        Assert.True(result.SuggestionsAvailable);
    }

    [Fact]
    public async Task PerspectiveDoesNotChangeTheNumbers()
    {
        var harness = new Harness();
        harness.Repo.Required.AddRange([Required(CSharp, "C#", 5), Required(Docker, "Docker", 4)]);
        harness.Repo.Held.Add(Held(CSharp, "C#", 4, verified: true));
        harness.Gemini.ResponseContent = """{ "suggestions": [] }""";

        var student = await harness.Service.AnalyzeAsync(StudentId, JobId, SkillGapPerspective.Student);
        var company = await harness.Service.AnalyzeAsync(StudentId, JobId, SkillGapPerspective.Company);

        Assert.Equal(student.Matched.Count, company.Matched.Count);
        Assert.Equal(student.Gap.Count, company.Gap.Count);
        Assert.Equal(student.MatchPercentage, company.MatchPercentage);
        Assert.Equal(SkillGapPerspective.Company, company.Perspective);
    }

    private static JobRequiredSkillRow Required(Guid id, string name, byte weight) =>
        new() { SkillId = id, SkillName = name, Domain = 0, Weight = weight };

    private static StudentHeldSkillRow Held(Guid id, string name, int proficiency, bool verified) =>
        new() { SkillId = id, SkillName = name, Domain = 0, ProficiencyLevel = proficiency, IsVerified = verified };

    private sealed class Harness
    {
        public StubSkillGapRepository Repo { get; } = new();
        public RecordingGemini Gemini { get; } = new();
        public SkillGapService Service { get; }

        public Harness()
        {
            Service = new SkillGapService(Repo, new FakeStudents(), Gemini, NullLogger<SkillGapService>.Instance);
        }
    }

    private sealed class FakeStudents : StubStudentRepository
    {
        public override Task<Student?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Student?>(new Student
            {
                Id = StudentId,
                UserId = StudentUserId,
                FirstName = "Tanvir",
                LastName = "Ahmed"
            });
    }

    private sealed class RecordingGemini : IGeminiClient
    {
        public int CallCount { get; private set; }
        public string ResponseContent { get; set; } = """{ "suggestions": [] }""";
        public string LastUserPrompt { get; private set; } = string.Empty;
        public bool LastJsonMode { get; private set; }
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
            LastUserPrompt = userPrompt;
            LastJsonMode = jsonMode;
            LastFeature = feature;
            LastUserId = userId;

            if (ThrowAiServiceException)
            {
                throw new AiServiceException("busy");
            }

            return Task.FromResult(new GeminiResponse(ResponseContent, 40, 90, 0.0003m));
        }

        public Task<GeminiResponse> GenerateChatAsync(
            string systemPrompt,
            IReadOnlyList<ChatMessage> history,
            IntegrationFeature feature,
            Guid userId,
            CancellationToken ct = default) => throw new NotSupportedException();
    }
}
