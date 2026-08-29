using System.Text;
using System.Text.Json;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.AI;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.SkillGap;

public class SkillGapService : ISkillGapService
{
    private const string StrictJsonReminder = " Respond with ONLY the JSON object, with no prose and no markdown fences.";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ISkillGapRepository _skillGap;
    private readonly IStudentRepository _students;
    private readonly IGeminiClient _gemini;
    private readonly ILogger<SkillGapService> _logger;

    public SkillGapService(
        ISkillGapRepository skillGap,
        IStudentRepository students,
        IGeminiClient gemini,
        ILogger<SkillGapService> logger)
    {
        _skillGap = skillGap;
        _students = students;
        _gemini = gemini;
        _logger = logger;
    }

    public async Task<SkillGapResult> AnalyzeAsync(
        Guid studentId,
        Guid jobId,
        SkillGapPerspective perspective,
        CancellationToken ct = default)
    {
        var required = await _skillGap.GetJobRequiredSkillsAsync(jobId, ct);
        var held = await _skillGap.GetStudentHeldSkillsAsync(studentId, ct);

        // ---------------------------------------------------------------------
        // Deterministic half. These four sets are pure relational set operations
        // keyed on SkillId, so they are exactly correct with zero AI involvement.
        // Determinism is the point: a student is never told they lack a skill
        // they hold because a model paraphrased the name.
        // ---------------------------------------------------------------------
        var heldById = held.ToDictionary(h => h.SkillId);

        var have = held.Select(h => new SkillGapSkill
        {
            SkillId = h.SkillId,
            SkillName = h.SkillName,
            Domain = (SkillDomain)h.Domain,
            ProficiencyLevel = h.ProficiencyLevel,
            IsVerified = h.IsVerified
        }).ToList();

        var needed = required.Select(r => new SkillGapSkill
        {
            SkillId = r.SkillId,
            SkillName = r.SkillName,
            Domain = (SkillDomain)r.Domain,
            Weight = r.Weight,
            ProficiencyLevel = heldById.TryGetValue(r.SkillId, out var match) ? match.ProficiencyLevel : null,
            IsVerified = heldById.TryGetValue(r.SkillId, out var verified) && verified.IsVerified
        }).ToList();

        var matched = needed.Where(n => heldById.ContainsKey(n.SkillId)).ToList();
        var gap = needed.Where(n => !heldById.ContainsKey(n.SkillId)).ToList();

        var result = new SkillGapResult
        {
            StudentId = studentId,
            JobId = jobId,
            Perspective = perspective,
            Have = have,
            Needed = needed,
            Matched = matched,
            Gap = gap
        };

        if (gap.Count == 0)
        {
            // Nothing missing, so there is nothing to ask the model about.
            result.SuggestionsAvailable = true;
            return result;
        }

        // ---------------------------------------------------------------------
        // AI half. Only the missing skill names leave the process: no resume, no
        // profile, no grades. A failure here degrades the panel, never blanks it.
        // ---------------------------------------------------------------------
        var suggestions = await GenerateSuggestionsAsync(studentId, gap, ct);
        if (suggestions is null)
        {
            return result;
        }

        var byName = suggestions
            .Where(s => !string.IsNullOrWhiteSpace(s.SkillName))
            .GroupBy(s => s.SkillName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var missing in gap)
        {
            if (byName.TryGetValue(missing.SkillName, out var suggestion))
            {
                missing.LearningResources = suggestion.LearningResources
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => r.Trim())
                    .ToList();
            }
        }

        result.SuggestionsAvailable = true;
        return result;
    }

    private async Task<List<SkillGapSuggestion>?> GenerateSuggestionsAsync(
        Guid studentId,
        IReadOnlyList<SkillGapSkill> gap,
        CancellationToken ct)
    {
        const string systemPrompt =
            "You point students at ways to learn skills they are missing for an internship. " +
            "Reply with JSON only: {\"suggestions\":[{\"skillName\":\"...\",\"learningResources\":[\"...\"]}]}. " +
            "Return one entry per skill you are given, and copy skillName back exactly as supplied. " +
            "Give two or three short, specific pointers per skill, naming a real course, book, docs site or " +
            "project idea, for example \"freeCodeCamp's Docker course\". " +
            "Do not include URLs and do not pad the list with generic advice like \"practise more\".";

        var userPrompt = new StringBuilder()
            .AppendLine("Missing skills:")
            .AppendLine(string.Join(Environment.NewLine, gap.Select(g => $"- {g.SkillName}")))
            .ToString();

        var userId = await ResolveUserIdAsync(studentId, ct);

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var prompt = attempt == 1 ? systemPrompt : systemPrompt + StrictJsonReminder;

            try
            {
                var response = await _gemini.GenerateAsync(
                    prompt,
                    userPrompt,
                    IntegrationFeature.SkillGap,
                    userId,
                    jsonMode: true,
                    ct);

                var batch = JsonSerializer.Deserialize<SkillGapSuggestionBatch>(
                    StripCodeFence(response.Content),
                    SerializerOptions);

                return batch?.Suggestions ?? [];
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Skill gap suggestions were unparseable on attempt {Attempt}.", attempt);
            }
            catch (AiServiceException ex)
            {
                _logger.LogWarning(ex, "Skill gap suggestions are unavailable.");
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
}
