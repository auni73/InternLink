using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.AI;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Resume;

public class ResumeAnalysisService : IResumeAnalysisService
{
    private const string StrictJsonReminder = " Respond with ONLY the JSON object, with no prose and no markdown fences.";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IResumeService _resumes;
    private readonly IStudentRepository _students;
    private readonly IJobRepository _jobs;
    private readonly IGeminiClient _gemini;
    private readonly ILogger<ResumeAnalysisService> _logger;

    public ResumeAnalysisService(
        IResumeService resumes,
        IStudentRepository students,
        IJobRepository jobs,
        IGeminiClient gemini,
        ILogger<ResumeAnalysisService> logger)
    {
        _resumes = resumes;
        _students = students;
        _jobs = jobs;
        _gemini = gemini;
        _logger = logger;
    }

    public async Task<AtsScoreResult> GetAtsScoreAsync(
        Guid resumeId,
        Guid studentId,
        Guid? targetJobId,
        CancellationToken ct = default)
    {
        // GetResumeForEditAsync is scoped to the student, so a foreign resume comes back null.
        var resume = await _resumes.GetResumeForEditAsync(resumeId, studentId, ct);
        if (resume is null)
        {
            return AtsScoreResult.Unavailable();
        }

        var systemPrompt =
            "You are an applicant tracking system auditing a student's resume. " +
            "Reply with JSON only: {\"atsScore\":<0-100>,\"grammarIssues\":[\"...\"]," +
            "\"structureCritique\":\"...\",\"missingKeywords\":[\"...\"]}. " +
            "atsScore reflects parseability, keyword coverage and measurable impact. " +
            "grammarIssues lists concrete wording or tense problems, quoting the offending phrase. " +
            "structureCritique is two or three sentences on layout, ordering and section completeness. " +
            "missingKeywords lists skills or terms an ATS would expect but cannot find.";

        var userPrompt = new StringBuilder()
            .AppendLine("Resume:")
            .AppendLine(DescribeResume(resume.Data))
            .ToString();

        if (targetJobId.HasValue && targetJobId.Value != Guid.Empty)
        {
            var job = await _jobs.GetByIdAsync(targetJobId.Value, ct);
            if (job is not null)
            {
                userPrompt +=
                    $"\nTarget role: {job.Title}\nDescription: {job.CoreDescription}\nCriteria: {job.SelectionCriteria}\n" +
                    "Score against this posting specifically.";
            }
        }

        var studentUserId = await ResolveUserIdAsync(studentId, ct);
        var result = await GenerateJsonAsync<AtsScoreResult>(
            systemPrompt,
            userPrompt,
            IntegrationFeature.AtsScoring,
            studentUserId,
            ct);

        if (result is null)
        {
            return AtsScoreResult.Unavailable();
        }

        result.AtsScore = Math.Clamp(result.AtsScore, 0, 100);
        return result;
    }

    public async Task<IReadOnlyList<ImprovementSuggestion>> GetImprovementSuggestionsAsync(
        Guid resumeId,
        Guid studentId,
        Guid targetJobId,
        CancellationToken ct = default)
    {
        var resume = await _resumes.GetResumeForEditAsync(resumeId, studentId, ct);
        if (resume is null)
        {
            return [];
        }

        var job = await _jobs.GetByIdAsync(targetJobId, ct);
        if (job is null)
        {
            return [];
        }

        const string systemPrompt =
            "You rewrite resume lines so they land better for a specific internship. " +
            "Reply with JSON only: {\"suggestions\":[{\"originalText\":\"...\",\"suggestedText\":\"...\",\"reason\":\"...\"}]}. " +
            "originalText must be copied verbatim from the resume so it can be located. " +
            "suggestedText is the improved line, quantified where possible and using the posting's vocabulary. " +
            "reason is one short sentence. Return between three and six suggestions.";

        var userPrompt = new StringBuilder()
            .AppendLine("Resume:")
            .AppendLine(DescribeResume(resume.Data))
            .AppendLine()
            .AppendLine($"Target role: {job.Title}")
            .AppendLine($"Description: {job.CoreDescription}")
            .AppendLine($"Criteria: {job.SelectionCriteria}")
            .ToString();

        var studentUserId = await ResolveUserIdAsync(studentId, ct);
        var batch = await GenerateJsonAsync<SuggestionBatch>(
            systemPrompt,
            userPrompt,
            IntegrationFeature.ResumeSuggestions,
            studentUserId,
            ct);

        return batch?.Suggestions?
            .Where(s => !string.IsNullOrWhiteSpace(s.SuggestedText))
            .ToList() ?? [];
    }

    /// <summary>
    /// Runs a JSON-mode call, retrying once with a stricter instruction before giving up,
    /// so a single malformed reply never surfaces as a broken page.
    /// </summary>
    private async Task<T?> GenerateJsonAsync<T>(
        string systemPrompt,
        string userPrompt,
        IntegrationFeature feature,
        Guid userId,
        CancellationToken ct)
        where T : class
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var prompt = attempt == 1 ? systemPrompt : systemPrompt + StrictJsonReminder;

            try
            {
                var response = await _gemini.GenerateAsync(prompt, userPrompt, feature, userId, jsonMode: true, ct);
                return JsonSerializer.Deserialize<T>(StripCodeFence(response.Content), SerializerOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "{Feature} returned unparseable JSON on attempt {Attempt}.",
                    feature,
                    attempt);
            }
            catch (AiServiceException ex)
            {
                _logger.LogWarning(ex, "{Feature} is unavailable.", feature);
                return null;
            }
        }

        return null;
    }

    /// <summary>Models sometimes wrap JSON in a fenced block even in JSON mode.</summary>
    private static string StripCodeFence(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
        {
            return trimmed;
        }

        var body = trimmed[(firstNewline + 1)..];
        var closing = body.LastIndexOf("```", StringComparison.Ordinal);
        return (closing >= 0 ? body[..closing] : body).Trim();
    }

    private async Task<Guid> ResolveUserIdAsync(Guid studentId, CancellationToken ct)
    {
        var student = await _students.GetByIdAsync(studentId, ct);
        return student?.UserId ?? Guid.Empty;
    }

    private static string DescribeResume(ResumeDataDto data)
    {
        var builder = new StringBuilder();
        var info = data.PersonalInfo;

        builder.AppendLine($"Name: {info.FullName}");
        builder.AppendLine($"Email: {info.Email}");
        builder.AppendLine($"Phone: {info.Phone}");
        builder.AppendLine($"Location: {info.Location}");

        // Contact links must be listed explicitly, otherwise the model reports them as missing.
        var links = new[] { info.LinkedIn, info.GitHub, info.Portfolio }
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
        if (links.Count > 0)
        {
            builder.AppendLine($"Links: {string.Join(", ", links)}");
        }

        if (!string.IsNullOrWhiteSpace(info.Summary))
        {
            builder.AppendLine($"Summary: {info.Summary}");
        }

        if (data.Education.Count > 0)
        {
            builder.AppendLine("Education:");
            foreach (var e in data.Education)
            {
                builder.AppendLine($"- {e.Degree} in {e.FieldOfStudy}, {e.Institution} ({DateRange(e.StartDate, e.EndDate, e.IsCurrent)}). GPA {e.Gpa}. {e.Highlights}");
            }
        }

        if (data.Experience.Count > 0)
        {
            builder.AppendLine("Experience:");
            foreach (var x in data.Experience)
            {
                builder.AppendLine($"- {x.Role} at {x.Company} ({DateRange(x.StartDate, x.EndDate, x.IsCurrent)}): {x.Description} {x.Highlights}");
            }
        }

        if (data.Projects.Count > 0)
        {
            builder.AppendLine("Projects:");
            foreach (var p in data.Projects)
            {
                builder.AppendLine($"- {p.Title} [{p.TechStack}]: {p.Description}");
            }
        }

        if (data.Skills.Count > 0)
        {
            builder.AppendLine($"Skills: {string.Join(", ", data.Skills.Select(s => s.SkillName))}");
        }

        return builder.ToString();
    }

    private static string DateRange(string start, string end, bool isCurrent) =>
        isCurrent ? $"{start} to Present" : $"{start} to {end}";

    private sealed class SuggestionBatch
    {
        [JsonPropertyName("suggestions")]
        public List<ImprovementSuggestion>? Suggestions { get; set; }
    }
}
