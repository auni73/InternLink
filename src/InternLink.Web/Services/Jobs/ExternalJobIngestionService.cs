using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.Vectors;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Jobs;

public class ExternalJobIngestionService : IExternalJobIngestionService
{
    private const string ArbeitnowSourceName = "Arbeitnow";
    private const string BdJobsSourceName = "BDJobs";
    private const string ArbeitnowApiUrl = "https://www.arbeitnow.com/api/job-board-api";
    private const string BdJobsApiUrl = "https://gateway.bdjobs.com/recruitment-account-test/api/JobSearch/GetJobSearch?isPro=1&rpp=20&pg=1";

    private readonly HttpClient _httpClient;
    private readonly IJobRepository _jobRepo;
    private readonly ISkillRepository _skillRepo;
    private readonly IJobIndexQueue _indexQueue;
    private readonly ILogger<ExternalJobIngestionService> _logger;

    public ExternalJobIngestionService(
        HttpClient httpClient,
        IJobRepository jobRepo,
        ISkillRepository skillRepo,
        IJobIndexQueue indexQueue,
        ILogger<ExternalJobIngestionService> logger)
    {
        _httpClient = httpClient;
        _jobRepo = jobRepo;
        _skillRepo = skillRepo;
        _indexQueue = indexQueue;
        _logger = logger;
    }

    public async Task<ExternalJobSyncResultDto> SyncAllSourcesAsync(CancellationToken ct = default)
    {
        var result = new ExternalJobSyncResultDto
        {
            SyncedAt = DateTimeOffset.UtcNow,
            SourcesQueried = [ArbeitnowSourceName, BdJobsSourceName]
        };

        // 1. Sync Arbeitnow
        var arbeitnowStats = await SyncArbeitnowAsync(ct);
        result.SourceStats.Add(arbeitnowStats);
        result.Ingested += arbeitnowStats.Ingested;
        result.Skipped += arbeitnowStats.Skipped;
        result.Failed += arbeitnowStats.Failed;

        // 2. Sync BDJobs
        var bdjobsStats = await SyncBdJobsAsync(ct);
        result.SourceStats.Add(bdjobsStats);
        result.Ingested += bdjobsStats.Ingested;
        result.Skipped += bdjobsStats.Skipped;
        result.Failed += bdjobsStats.Failed;

        _logger.LogInformation(
            "External Job Ingestion completed: {Ingested} ingested, {Skipped} skipped, {Failed} failed.",
            result.Ingested, result.Skipped, result.Failed);

        return result;
    }

    public async Task<ExternalJobSourceSyncStatsDto> SyncArbeitnowAsync(CancellationToken ct = default)
    {
        var stats = new ExternalJobSourceSyncStatsDto { SourceName = ArbeitnowSourceName };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ArbeitnowApiUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124.0.0.0 Safari/537.36");

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                stats.ErrorMessage = $"Arbeitnow API returned HTTP {(int)response.StatusCode}";
                _logger.LogWarning("Arbeitnow API failed with status {StatusCode}", response.StatusCode);
                return stats;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Array)
            {
                stats.ErrorMessage = "Arbeitnow response did not contain a data array.";
                return stats;
            }

            var allSkills = await _skillRepo.GetAllAsync(ct);

            foreach (var item in dataElement.EnumerateArray())
            {
                try
                {
                    var slug = GetStringProperty(item, "slug");
                    var title = GetStringProperty(item, "title");
                    var companyName = GetStringProperty(item, "company_name");
                    var url = GetStringProperty(item, "url");
                    var rawDescription = GetStringProperty(item, "description");
                    var isRemote = item.TryGetProperty("remote", out var remElem) && remElem.GetBoolean();
                    var locationStr = GetStringProperty(item, "location");

                    if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(title))
                    {
                        continue;
                    }

                    var externalJobId = $"arbeitnow-{slug}";
                    if (await _jobRepo.ExistsExternalJobAsync(ArbeitnowSourceName, externalJobId, ct))
                    {
                        stats.Skipped++;
                        continue;
                    }

                    var cleanedDesc = StripHtmlTags(rawDescription);
                    if (string.IsNullOrWhiteSpace(cleanedDesc))
                    {
                        cleanedDesc = $"{title} position at {companyName}. Visit the original posting for details.";
                    }

                    // Extract tags
                    var tags = new List<string>();
                    if (item.TryGetProperty("tags", out var tagsElem) && tagsElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var tag in tagsElem.EnumerateArray())
                        {
                            var t = tag.GetString();
                            if (!string.IsNullOrWhiteSpace(t)) tags.Add(t);
                        }
                    }

                    var matchedSkillIds = MatchSkills(allSkills, tags, $"{title} {cleanedDesc}");

                    var locationType = isRemote 
                        ? LocationType.Remote 
                        : (locationStr?.Contains("hybrid", StringComparison.OrdinalIgnoreCase) == true ? LocationType.Hybrid : LocationType.OnSite);

                    DateTimeOffset postedAt = DateTimeOffset.UtcNow;
                    if (item.TryGetProperty("created_at", out var catElem) && catElem.TryGetInt64(out var epochSec) && epochSec > 0)
                    {
                        try { postedAt = DateTimeOffset.FromUnixTimeSeconds(epochSec); } catch { postedAt = DateTimeOffset.UtcNow; }
                    }

                    var deadline = (postedAt.AddDays(45) > DateTimeOffset.UtcNow) 
                        ? postedAt.AddDays(45) 
                        : DateTimeOffset.UtcNow.AddDays(30);

                    var job = new Job
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = null,
                        Title = Truncate(title, 200),
                        CompanyNameSnapshot = Truncate(companyName ?? "Technology Partner", 200),
                        CoreDescription = cleanedDesc,
                        SelectionCriteria = tags.Count > 0 ? $"Tags: {string.Join(", ", tags)}" : "See source circular description.",
                        LocationType = locationType,
                        DeadLine = deadline,
                        CreatedAt = postedAt,
                        IsApproved = true,
                        IsClosed = false,
                        Source = JobSource.External,
                        ExternalSourceName = ArbeitnowSourceName,
                        ExternalJobId = externalJobId,
                        ExternalApplyUrl = url ?? $"https://www.arbeitnow.com/jobs/{slug}",
                        LastSyncedAt = DateTimeOffset.UtcNow
                    };

                    var createdId = await _jobRepo.CreateExternalJobAsync(
                        job, 
                        matchedSkillIds.Select(s => (s, 3)), 
                        ct);

                    _indexQueue.TryEnqueue(new JobIndexCommand(createdId, JobIndexOperation.Upsert));
                    stats.Ingested++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to ingest single Arbeitnow job");
                    stats.Failed++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync Arbeitnow jobs");
            stats.ErrorMessage = ex.Message;
            stats.Failed++;
        }

        return stats;
    }

    public async Task<ExternalJobSourceSyncStatsDto> SyncBdJobsAsync(CancellationToken ct = default)
    {
        var stats = new ExternalJobSourceSyncStatsDto { SourceName = BdJobsSourceName };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BdJobsApiUrl);
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124.0.0.0 Safari/537.36");

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                stats.ErrorMessage = $"BDJobs gateway returned HTTP {(int)response.StatusCode}";
                _logger.LogWarning("BDJobs gateway failed with status {StatusCode}", response.StatusCode);
                return stats;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            JsonElement itemsElement;
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                itemsElement = data;
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                itemsElement = doc.RootElement;
            }
            else
            {
                stats.ErrorMessage = "BDJobs response format unrecognized.";
                return stats;
            }

            var allSkills = await _skillRepo.GetAllAsync(ct);

            foreach (var item in itemsElement.EnumerateArray())
            {
                try
                {
                    var jobIdStr = GetCaseInsensitiveProperty(item, "Jobid", "jobid", "jobId", "JobId");
                    var title = GetCaseInsensitiveProperty(item, "jobTitle", "JobTitle", "title");
                    var companyName = GetCaseInsensitiveProperty(item, "companyName", "CompanyName");
                    var deadlineStr = GetCaseInsensitiveProperty(item, "deadlineDB", "deadline", "Deadline");
                    var eduRec = GetCaseInsensitiveProperty(item, "eduRec", "education");
                    var experience = GetCaseInsensitiveProperty(item, "experience");
                    var location = GetCaseInsensitiveProperty(item, "location");
                    var description = GetCaseInsensitiveProperty(item, "jobDescription", "description");

                    if (string.IsNullOrWhiteSpace(jobIdStr) || string.IsNullOrWhiteSpace(title))
                    {
                        continue;
                    }

                    var externalJobId = $"bdjobs-{jobIdStr.Trim()}";
                    if (await _jobRepo.ExistsExternalJobAsync(BdJobsSourceName, externalJobId, ct))
                    {
                        stats.Skipped++;
                        continue;
                    }

                    // Live verification on BDJobs portal to ensure circular is active and not defunct/deleted
                    var detailsEndpoint = $"https://gateway.bdjobs.com/jobapply/api/JobSubsystem/Job-Details?jobid={jobIdStr.Trim()}";
                    try
                    {
                        using var detailsReq = new HttpRequestMessage(HttpMethod.Get, detailsEndpoint);
                        detailsReq.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124.0.0.0 Safari/537.36");
                        using var detailsResp = await _httpClient.SendAsync(detailsReq, ct);
                        if (detailsResp.IsSuccessStatusCode)
                        {
                            var detailsJson = await detailsResp.Content.ReadAsStringAsync(ct);
                            using var detailsDoc = JsonDocument.Parse(detailsJson);
                            if (!detailsDoc.RootElement.TryGetProperty("data", out var dataProp) || dataProp.ValueKind == JsonValueKind.Null)
                            {
                                // Job circular is defunct or not found on BDJobs live portal (e.g. statuscode != 0 or data == null)
                                _logger.LogInformation("BDJobs circular {JobId} ({Title}) is unavailable on BDJobs; skipping.", jobIdStr, title);
                                stats.Skipped++;
                                continue;
                            }

                            if (dataProp.TryGetProperty("Closed", out var closedElem))
                            {
                                if ((closedElem.ValueKind == JsonValueKind.Number && closedElem.GetInt32() == 1) ||
                                    closedElem.ValueKind == JsonValueKind.True ||
                                    (closedElem.ValueKind == JsonValueKind.String && closedElem.GetString() == "1"))
                                {
                                    _logger.LogInformation("BDJobs circular {JobId} is closed; skipping.", jobIdStr);
                                    stats.Skipped++;
                                    continue;
                                }
                            }

                            var fullDesc = GetCaseInsensitiveProperty(dataProp, "JobDescription");
                            if (!string.IsNullOrWhiteSpace(fullDesc))
                            {
                                description = fullDesc;
                            }
                            var addlReq = GetCaseInsensitiveProperty(dataProp, "AdditionJobRequirements");
                            if (!string.IsNullOrWhiteSpace(addlReq))
                            {
                                eduRec = string.IsNullOrWhiteSpace(eduRec) ? addlReq : $"{eduRec}\n{addlReq}";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Live verification check failed for BDJobs {JobId}, continuing with search data", jobIdStr);
                    }

                    var cleanedDesc = StripHtmlTags(description);
                    if (string.IsNullOrWhiteSpace(cleanedDesc))
                    {
                        cleanedDesc = $"{title} opportunity at {companyName}. Location: {location ?? "Bangladesh"}.";
                    }

                    var criteriaParts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(eduRec)) criteriaParts.Add($"Education: {StripHtmlTags(eduRec)}");
                    if (!string.IsNullOrWhiteSpace(experience)) criteriaParts.Add($"Experience: {StripHtmlTags(experience)}");
                    if (!string.IsNullOrWhiteSpace(location)) criteriaParts.Add($"Location: {StripHtmlTags(location)}");
                    var selectionCriteria = criteriaParts.Count > 0 ? string.Join("\n", criteriaParts) : "See circular on BDJobs.";

                    DateTimeOffset deadline = DateTimeOffset.UtcNow.AddDays(30);
                    if (!string.IsNullOrWhiteSpace(deadlineStr) && DateTimeOffset.TryParse(deadlineStr, out var parsedDeadline))
                    {
                        if (parsedDeadline <= DateTimeOffset.UtcNow)
                        {
                            // Postings with past deadlines are expired on BDJobs — skip ingestion
                            stats.Skipped++;
                            continue;
                        }
                        deadline = parsedDeadline;
                    }

                    DateTimeOffset postedAt = DateTimeOffset.UtcNow;
                    var publishDateStr = GetCaseInsensitiveProperty(item, "publishDate", "PublishDate");
                    if (!string.IsNullOrWhiteSpace(publishDateStr) && DateTimeOffset.TryParse(publishDateStr, out var parsedPublishDate))
                    {
                        postedAt = parsedPublishDate;
                    }

                    var matchedSkillIds = MatchSkills(allSkills, [], $"{title} {cleanedDesc} {eduRec} {experience}");

                    var locationType = (location?.Contains("remote", StringComparison.OrdinalIgnoreCase) == true)
                        ? LocationType.Remote
                        : (location?.Contains("hybrid", StringComparison.OrdinalIgnoreCase) == true ? LocationType.Hybrid : LocationType.OnSite);

                    var job = new Job
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = null,
                        Title = Truncate(title, 200),
                        CompanyNameSnapshot = Truncate(companyName ?? "Bangladesh Employer", 200),
                        CoreDescription = cleanedDesc,
                        SelectionCriteria = selectionCriteria,
                        LocationType = locationType,
                        DeadLine = deadline,
                        CreatedAt = postedAt,
                        IsApproved = true,
                        IsClosed = false,
                        Source = JobSource.External,
                        ExternalSourceName = BdJobsSourceName,
                        ExternalJobId = externalJobId,
                        ExternalApplyUrl = $"https://bdjobs.com/h/details/{jobIdStr.Trim()}?ln=1",
                        LastSyncedAt = DateTimeOffset.UtcNow
                    };

                    var createdId = await _jobRepo.CreateExternalJobAsync(
                        job, 
                        matchedSkillIds.Select(s => (s, 3)), 
                        ct);

                    _indexQueue.TryEnqueue(new JobIndexCommand(createdId, JobIndexOperation.Upsert));
                    stats.Ingested++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to ingest single BDJobs posting");
                    stats.Failed++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync BDJobs postings");
            stats.ErrorMessage = ex.Message;
            stats.Failed++;
        }

        return stats;
    }

    private static List<Guid> MatchSkills(IReadOnlyList<Skill> allSkills, IEnumerable<string> explicitTags, string textContent)
    {
        var matched = new HashSet<Guid>();
        var content = textContent.ToLowerInvariant();
        var tagSet = explicitTags.Select(t => t.Trim().ToLowerInvariant()).ToHashSet();

        foreach (var skill in allSkills)
        {
            var skillLower = skill.SkillName.ToLowerInvariant();

            // 1. Direct tag match
            if (tagSet.Contains(skillLower))
            {
                matched.Add(skill.Id);
                continue;
            }

            // 2. Exact word boundary match in content
            var pattern = $@"\b{Regex.Escape(skillLower)}\b";
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
            {
                matched.Add(skill.Id);
            }
        }

        return matched.ToList();
    }

    private static string? GetStringProperty(JsonElement element, string propName)
    {
        if (element.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    private static string? GetCaseInsensitiveProperty(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.String) return prop.GetString();
                if (prop.ValueKind == JsonValueKind.Number) return prop.GetInt64().ToString();
            }
        }
        return null;
    }

    private static string StripHtmlTags(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var decoded = System.Net.WebUtility.HtmlDecode(html);
        var noTags = Regex.Replace(decoded, @"<[^>]*>", " ");
        return Regex.Replace(noTags, @"\s+", " ").Trim();
    }

    private static string Truncate(string str, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(str)) return string.Empty;
        return str.Length <= maxLength ? str : str[..maxLength];
    }
}
