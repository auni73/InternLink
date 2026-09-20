using System.Text.Json;
using System.Text.RegularExpressions;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.AI;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Jobs;

public class ExternalJobParseService : IExternalJobParseService
{
    private readonly IGeminiClient _geminiClient;
    private readonly ISkillRepository _skillRepo;
    private readonly ILogger<ExternalJobParseService> _logger;

    public ExternalJobParseService(
        IGeminiClient geminiClient,
        ISkillRepository skillRepo,
        ILogger<ExternalJobParseService> logger)
    {
        _geminiClient = geminiClient;
        _skillRepo = skillRepo;
        _logger = logger;
    }

    public async Task<ParseExternalJobResponseDto> ParseCircularAsync(
        string rawContent, 
        Guid adminUserId, 
        string? sourceName = null, 
        string? sourceUrl = null, 
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return new ParseExternalJobResponseDto();
        }

        var availableSkills = await _skillRepo.GetAllAsync(ct);
        var skillNames = availableSkills.Select(s => s.SkillName).ToList();

        const string systemPrompt = @"
You are an expert recruitment parser for a university career portal.
Extract structured hiring information from unstructured job circulars, flyers, newspaper clippings, or social media posts.
Return valid JSON adhering strictly to this schema:
{
  ""title"": ""Job or Internship Title (max 100 chars)"",
  ""companyNameSnapshot"": ""Company / Organization Name (max 100 chars)"",
  ""locationType"": 0 for Remote, 1 for OnSite, 2 for Hybrid,
  ""deadLineIso"": ""YYYY-MM-DD (or null if not found)"",
  ""coreDescription"": ""Comprehensive summary of role, duties, and responsibilities."",
  ""selectionCriteria"": ""Required educational qualifications, degree, CGPA, and prerequisites."",
  ""suggestedSkillNames"": [""Skill1"", ""Skill2""]
}
Only suggest relevant technical and soft skills. Keep fields realistic and professional.";

        var userPrompt = $@"
Raw Job Circular Text:
---
{rawContent}
---
{(string.IsNullOrWhiteSpace(sourceName) ? "" : $"Reported Source: {sourceName}\n")}
{(string.IsNullOrWhiteSpace(sourceUrl) ? "" : $"Reported URL: {sourceUrl}\n")}
Known platform skills to prioritize: {string.Join(", ", skillNames)}";

        try
        {
            var response = await _geminiClient.GenerateAsync(
                systemPrompt,
                userPrompt,
                IntegrationFeature.JobIngestionParsing,
                adminUserId,
                jsonMode: true,
                ct: ct);

            using var doc = JsonDocument.Parse(response.Content);
            var root = doc.RootElement;

            var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
            var company = root.TryGetProperty("companyNameSnapshot", out var c) ? c.GetString() : null;
            var locationTypeVal = root.TryGetProperty("locationType", out var lt) && lt.TryGetInt32(out var lti) ? lti : 1;
            var deadLineStr = root.TryGetProperty("deadLineIso", out var dl) ? dl.GetString() : null;
            var coreDesc = root.TryGetProperty("coreDescription", out var cd) ? cd.GetString() : null;
            var criteria = root.TryGetProperty("selectionCriteria", out var sc) ? sc.GetString() : null;

            var skills = new List<string>();
            if (root.TryGetProperty("suggestedSkillNames", out var skArr) && skArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in skArr.EnumerateArray())
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) skills.Add(s);
                }
            }

            DateTimeOffset? parsedDeadline = null;
            if (!string.IsNullOrWhiteSpace(deadLineStr) && DateTimeOffset.TryParse(deadLineStr, out var dlo))
            {
                parsedDeadline = dlo;
            }

            return new ParseExternalJobResponseDto
            {
                Title = title ?? string.Empty,
                CompanyNameSnapshot = company ?? string.Empty,
                LocationType = Enum.IsDefined(typeof(LocationType), (byte)locationTypeVal) ? (LocationType)(byte)locationTypeVal : LocationType.OnSite,
                DeadLine = parsedDeadline,
                CoreDescription = coreDesc ?? string.Empty,
                SelectionCriteria = criteria ?? string.Empty,
                SuggestedSkillNames = skills
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini circular parsing failed or unavailable. Falling back to heuristic text extraction.");
            return FallbackHeuristicParse(rawContent, skillNames);
        }
    }

    private static ParseExternalJobResponseDto FallbackHeuristicParse(string rawContent, List<string> knownSkills)
    {
        var lines = rawContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var title = lines.Length > 0 ? lines[0] : "New Opportunity";
        var company = lines.Length > 1 ? lines[1] : "Hiring Company";

        var matchedSkills = new List<string>();
        foreach (var skill in knownSkills)
        {
            if (Regex.IsMatch(rawContent, $@"(?<!\w){Regex.Escape(skill)}(?!\w)", RegexOptions.IgnoreCase))
            {
                matchedSkills.Add(skill);
            }
        }

        var isRemote = rawContent.Contains("remote", StringComparison.OrdinalIgnoreCase);
        var isHybrid = rawContent.Contains("hybrid", StringComparison.OrdinalIgnoreCase);

        return new ParseExternalJobResponseDto
        {
            Title = title.Length > 100 ? title[..100] : title,
            CompanyNameSnapshot = company.Length > 100 ? company[..100] : company,
            LocationType = isRemote ? LocationType.Remote : (isHybrid ? LocationType.Hybrid : LocationType.OnSite),
            DeadLine = DateTimeOffset.UtcNow.AddDays(30),
            CoreDescription = rawContent,
            SelectionCriteria = "Parsed from circular. Please review prerequisites.",
            SuggestedSkillNames = matchedSkills
        };
    }
}
