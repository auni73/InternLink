using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.AI;
using InternLink.Web.Services.Vectors;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Recommendation;

public class RecommendationService : IRecommendationService
{
    private const int VectorTopK = 20;
    private const int MaxResults = 10;
    private const int BiographyExcerptLength = 400;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(1);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IStudentRepository _students;
    private readonly IJobRepository _jobs;
    private readonly IEmbeddingClient _embedder;
    private readonly IVectorSearch _vectorSearch;
    private readonly IGeminiClient _gemini;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        IStudentRepository students,
        IJobRepository jobs,
        IEmbeddingClient embedder,
        IVectorSearch vectorSearch,
        IGeminiClient gemini,
        IMemoryCache cache,
        ILogger<RecommendationService> logger)
    {
        _students = students;
        _jobs = jobs;
        _embedder = embedder;
        _vectorSearch = vectorSearch;
        _gemini = gemini;
        _cache = cache;
        _logger = logger;
    }

    public async Task<RecommendationResultViewModel> GetRecommendedJobsAsync(Guid studentId, CancellationToken ct = default)
    {
        var student = await _students.GetByIdAsync(studentId, ct);
        if (student is null)
        {
            return new RecommendationResultViewModel();
        }

        var skills = await _students.GetStudentSkillsAsync(studentId, ct);
        var cacheKey = BuildCacheKey(studentId, skills);

        if (_cache.TryGetValue(cacheKey, out RecommendationResultViewModel? cached) && cached is not null)
        {
            _logger.LogInformation("Serving cached recommendations for student {StudentId}.", studentId);
            return cached;
        }

        RecommendationResultViewModel result;
        try
        {
            result = await BuildSemanticAsync(student, skills, ct);
        }
        catch (SemanticSearchUnavailableException ex)
        {
            _logger.LogWarning(ex, "Semantic search unavailable; using relational skill overlap for student {StudentId}.", studentId);
            result = await BuildFallbackAsync(student, ct);
        }

        _cache.Set(cacheKey, result, CacheLifetime);
        return result;
    }

    private async Task<RecommendationResultViewModel> BuildSemanticAsync(
        Student student,
        IReadOnlyList<StudentSkill> skills,
        CancellationToken ct)
    {
        var queryVector = await _embedder.EmbedAsync(BuildQueryText(student, skills), EmbeddingPurpose.Query, ct);
        var hits = await _vectorSearch.SearchJobsAsync(queryVector, VectorTopK, ct);

        if (hits.Count == 0)
        {
            return new RecommendationResultViewModel();
        }

        var scoreByJobId = hits.ToDictionary(h => h.JobId, h => h.Score);
        var candidates = await _jobs.GetRecommendationCandidatesAsync([.. scoreByJobId.Keys], student.Id, ct);

        var ranked = candidates
            .Where(c => !c.HasApplied)
            .Select(c => (Candidate: c, Score: HybridScore(scoreByJobId.GetValueOrDefault(c.JobId), c)))
            .OrderByDescending(x => x.Score)
            .Take(MaxResults)
            .ToList();

        var jobs = ranked.Select(x => ToViewModel(x.Candidate, x.Score)).ToList();
        var topSkills = ranked.ToDictionary(x => x.Candidate.JobId, x => x.Candidate.TopMatchedSkillName);

        await ApplyReasonsAsync(student, jobs, topSkills, ct);

        return new RecommendationResultViewModel { Degraded = false, Jobs = jobs };
    }

    private async Task<RecommendationResultViewModel> BuildFallbackAsync(Student student, CancellationToken ct)
    {
        var candidates = await _jobs.GetSkillOverlapRankedJobsAsync(student.Id, MaxResults * 2, ct);

        var ranked = candidates
            .Where(c => !c.HasApplied)
            .Take(MaxResults)
            .ToList();

        // No embeddings available here, so the reason writer is skipped entirely and templates are used.
        var jobs = ranked.Select(c =>
        {
            var vm = ToViewModel(c, SkillRatio(c));
            vm.Reason = TemplateReason(vm, c.TopMatchedSkillName);
            return vm;
        }).ToList();

        return new RecommendationResultViewModel { Degraded = true, Jobs = jobs };
    }

    private static double HybridScore(float cosineScore, RecommendationCandidate candidate)
    {
        var cosine = Math.Clamp(cosineScore, 0d, 1d);
        return (0.7 * cosine) + (0.3 * SkillRatio(candidate));
    }

    private static double SkillRatio(RecommendationCandidate candidate) =>
        candidate.RequiredSkillCount > 0
            ? (double)candidate.MatchedSkillCount / candidate.RequiredSkillCount
            : 0d;

    private static RecommendedJobViewModel ToViewModel(RecommendationCandidate candidate, double score) => new()
    {
        JobId = candidate.JobId,
        Title = candidate.Title,
        CompanyName = candidate.CompanyName,
        LocationType = candidate.LocationType.ToString(),
        DeadLine = candidate.DeadLine,
        MatchedSkillCount = candidate.MatchedSkillCount,
        RequiredSkillCount = candidate.RequiredSkillCount,
        MatchPercentage = Math.Clamp((int)Math.Round(score * 100), 0, 100),
        Reason = string.Empty
    };

    internal static string BuildQueryText(Student student, IReadOnlyList<StudentSkill> skills)
    {
        var skillText = skills.Count > 0
            ? string.Join(", ", skills.Select(s => $"{s.Skill.SkillName} (level {s.ProficiencyLevel}/5)"))
            : "none recorded";

        var biography = student.Biography ?? string.Empty;
        if (biography.Length > BiographyExcerptLength)
        {
            biography = biography[..BiographyExcerptLength];
        }

        return $"{student.Department}. Skills: {skillText}. Interests: {student.Interests}. {biography}".Trim();
    }

    internal static string BuildCacheKey(Guid studentId, IReadOnlyList<StudentSkill> skills)
    {
        // Hashing the skill set means editing a skill naturally invalidates the entry.
        var canonical = string.Join(
            ",",
            skills.Select(s => $"{s.SkillId:N}:{s.ProficiencyLevel}").OrderBy(x => x, StringComparer.Ordinal));

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..16];
        return $"recommendations:{studentId:N}:{hash}";
    }

    internal static string TemplateReason(RecommendedJobViewModel job, string? topSkillName = null) =>
        string.IsNullOrWhiteSpace(topSkillName)
            ? $"Matches {job.MatchedSkillCount} of the {job.RequiredSkillCount} skills this role asks for."
            : $"Matches {job.MatchedSkillCount} of your skills including {topSkillName}.";

    private async Task ApplyReasonsAsync(
        Student student,
        IReadOnlyList<RecommendedJobViewModel> jobs,
        IReadOnlyDictionary<Guid, string?> topSkills,
        CancellationToken ct)
    {
        if (jobs.Count == 0)
        {
            return;
        }

        var reasons = await TryGenerateReasonsAsync(student, jobs, ct);

        foreach (var job in jobs)
        {
            job.Reason = reasons.TryGetValue(job.JobId, out var reason) && !string.IsNullOrWhiteSpace(reason)
                ? reason
                : TemplateReason(job, topSkills.GetValueOrDefault(job.JobId));
        }
    }

    private async Task<Dictionary<Guid, string>> TryGenerateReasonsAsync(
        Student student,
        IReadOnlyList<RecommendedJobViewModel> jobs,
        CancellationToken ct)
    {
        const string systemPrompt =
            "You explain why an internship suits a specific student. " +
            "Reply with JSON only: {\"reasons\":[{\"jobId\":\"<id>\",\"reason\":\"<one sentence>\"}]}. " +
            "Return exactly one entry per supplied jobId, reusing the ids verbatim. " +
            "Each reason must be one specific sentence under 25 words referencing the student's skills or interests.";

        var jobLines = jobs.Select(j =>
            $"- jobId={j.JobId}; title={j.Title}; company={j.CompanyName}; matchedSkills={j.MatchedSkillCount}/{j.RequiredSkillCount}");

        var userPrompt =
            $"Student: {student.Department}. Interests: {student.Interests}.\nJobs:\n{string.Join("\n", jobLines)}";

        try
        {
            var response = await _gemini.GenerateAsync(
                systemPrompt,
                userPrompt,
                IntegrationFeature.JobRecommendations,
                student.UserId,
                jsonMode: true,
                ct);

            var parsed = JsonSerializer.Deserialize<ReasonBatchResponse>(response.Content, SerializerOptions);
            if (parsed?.Reasons is null)
            {
                return [];
            }

            // Models occasionally invent ids, so only accept ones we actually sent.
            var requested = jobs.Select(j => j.JobId).ToHashSet();

            return parsed.Reasons
                .Where(r => Guid.TryParse(r.JobId, out var id) && requested.Contains(id))
                .GroupBy(r => Guid.Parse(r.JobId!))
                .ToDictionary(g => g.Key, g => g.First().Reason ?? string.Empty);
        }
        catch (AiServiceException ex)
        {
            _logger.LogWarning(ex, "Reason generation unavailable; falling back to template reasons.");
            return [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Reason generation returned unreadable JSON; falling back to template reasons.");
            return [];
        }
    }

    private sealed class ReasonBatchResponse
    {
        [JsonPropertyName("reasons")]
        public List<ReasonItem>? Reasons { get; set; }
    }

    private sealed class ReasonItem
    {
        [JsonPropertyName("jobId")]
        public string? JobId { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }
}
