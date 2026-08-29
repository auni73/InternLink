using System.Text.Json;
using System.Text.Json.Nodes;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.Storage;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Resume;

public class ResumeService : IResumeService
{
    private readonly IResumeRepository _resumeRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly ISkillRepository _skillRepository;
    private readonly IPdfRenderer _pdfRenderer;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<ResumeService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ResumeService(
        IResumeRepository resumeRepository,
        IStudentRepository studentRepository,
        ISkillRepository skillRepository,
        IPdfRenderer pdfRenderer,
        IFileStorage fileStorage,
        ILogger<ResumeService> logger)
    {
        _resumeRepository = resumeRepository;
        _studentRepository = studentRepository;
        _skillRepository = skillRepository;
        _pdfRenderer = pdfRenderer;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ResumeItemViewModel>> GetStudentResumesAsync(Guid studentId, CancellationToken ct = default)
    {
        var resumes = await _resumeRepository.GetByStudentIdAsync(studentId, ct);
        var result = new List<ResumeItemViewModel>();

        foreach (var r in resumes)
        {
            var data = ParseResumeData(r.DynamicJsonData);
            result.Add(new ResumeItemViewModel
            {
                Id = r.Id,
                StudentId = r.StudentId,
                DocumentPath = r.DocumentPath,
                LastModified = r.LastModified,
                FullName = data.PersonalInfo?.FullName ?? "Draft Resume",
                SkillCount = data.Skills?.Count ?? 0,
                ExperienceCount = data.Experience?.Count ?? 0
            });
        }

        return result;
    }

    public async Task<ResumeBuilderViewModel?> GetResumeForEditAsync(Guid resumeId, Guid studentId, CancellationToken ct = default)
    {
        var resume = await _resumeRepository.GetByIdAsync(resumeId, ct);
        if (resume is null || resume.StudentId != studentId)
        {
            return null;
        }

        var availableSkills = await _skillRepository.GetAllAsync(ct);
        var data = ParseResumeData(resume.DynamicJsonData);

        return new ResumeBuilderViewModel
        {
            ResumeId = resume.Id,
            StudentId = resume.StudentId,
            IsFinalized = !string.IsNullOrWhiteSpace(resume.DocumentPath),
            Data = data,
            AvailableSkills = availableSkills
        };
    }

    public async Task<Models.Resume> CreateDraftResumeAsync(Guid studentId, CancellationToken ct = default)
    {
        var student = await _studentRepository.GetByIdAsync(studentId, ct);
        var resume = await _resumeRepository.CreateAsync(studentId, ct);

        // Prepopulate personal-info from student profile if available
        if (student != null)
        {
            var initialData = new ResumeDataDto
            {
                PersonalInfo = new PersonalInfoStepDto
                {
                    FullName = $"{student.FirstName} {student.LastName}".Trim(),
                    Summary = student.Biography
                }
            };

            var json = JsonSerializer.Serialize(initialData, JsonOptions);
            await _resumeRepository.UpdateDynamicJsonDataAsync(resume.Id, json, DateTimeOffset.UtcNow, ct);
            resume.DynamicJsonData = json;
        }

        return resume;
    }

    public async Task<bool> UpdateStepAsync(
        Guid resumeId, 
        Guid studentId, 
        string stepName, 
        string stepJson, 
        CancellationToken ct = default)
    {
        var resume = await _resumeRepository.GetByIdAsync(resumeId, ct);
        if (resume is null || resume.StudentId != studentId)
        {
            return false;
        }

        // Read-modify-write in C# using JsonObject:
        // Preserves all other top-level keys in DynamicJsonData while replacing/merging only the targeted step's section.
        var rootNode = JsonNode.Parse(string.IsNullOrWhiteSpace(resume.DynamicJsonData) ? "{}" : resume.DynamicJsonData) as JsonObject 
                       ?? new JsonObject();
        var incomingNode = JsonNode.Parse(string.IsNullOrWhiteSpace(stepJson) ? "{}" : stepJson);

        var normalizedStep = stepName.ToLowerInvariant().Trim();
        switch (normalizedStep)
        {
            case "personal-info":
            case "personalinfo":
                rootNode["personalInfo"] = incomingNode;
                break;

            case "education":
                rootNode["education"] = incomingNode;
                break;

            case "experience":
                rootNode["experience"] = incomingNode;
                break;

            case "skills":
                rootNode["skills"] = incomingNode;
                // Also upsert real relational StudentSkills in a transaction
                try
                {
                    var skillsList = JsonSerializer.Deserialize<List<SkillEntryDto>>(stepJson, JsonOptions);
                    if (skillsList != null)
                    {
                        var relationalPairs = skillsList
                            .Where(s => s.SkillId != Guid.Empty)
                            .Select(s => (s.SkillId, s.ProficiencyLevel));
                        await _studentRepository.SyncStudentSkillsAsync(studentId, relationalPairs, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error deserializing and syncing student skills for resume {ResumeId}", resumeId);
                }
                break;

            case "projects":
                rootNode["projects"] = incomingNode;
                break;

            default:
                _logger.LogWarning("Unknown resume wizard step '{StepName}' requested for resume {ResumeId}", stepName, resumeId);
                return false;
        }

        var updatedJson = rootNode.ToJsonString(JsonOptions);
        var lastModified = DateTimeOffset.UtcNow;
        await _resumeRepository.UpdateDynamicJsonDataAsync(resumeId, updatedJson, lastModified, ct);

        return true;
    }

    public async Task<(bool Success, string? DocumentPath, string? ErrorMessage)> FinalizeResumeAsync(
        Guid resumeId, 
        Guid studentId, 
        CancellationToken ct = default)
    {
        var resume = await _resumeRepository.GetByIdAsync(resumeId, ct);
        if (resume is null || resume.StudentId != studentId)
        {
            return (false, null, "Resume not found or access denied.");
        }

        var resumeData = ParseResumeData(resume.DynamicJsonData);
        if (string.IsNullOrWhiteSpace(resumeData.PersonalInfo?.FullName))
        {
            return (false, null, "Please complete the Personal Info step with your name before finalizing.");
        }

        // Render PDF in memory via QuestPDF
        var pdfBytes = _pdfRenderer.RenderResumePdf(resumeData);

        // Save to disk under Storage:ResumeRoot/{studentId}/{resumeId}.pdf
        var filePath = await _fileStorage.SaveResumePdfAsync(studentId, resumeId, pdfBytes, ct);

        // Update DocumentPath in DB
        await _resumeRepository.FinalizeAsync(resumeId, filePath, DateTimeOffset.UtcNow, ct);

        return (true, filePath, null);
    }

    public async Task<(Stream? Stream, string FileName)> OpenDownloadStreamAsync(
        Guid resumeId, 
        Guid studentId, 
        CancellationToken ct = default)
    {
        var resume = await _resumeRepository.GetByIdAsync(resumeId, ct);
        if (resume is null || resume.StudentId != studentId || string.IsNullOrWhiteSpace(resume.DocumentPath))
        {
            return (null, string.Empty);
        }

        var stream = await _fileStorage.OpenReadAsync(resume.DocumentPath, ct);
        if (stream is null)
        {
            return (null, string.Empty);
        }

        var data = ParseResumeData(resume.DynamicJsonData);
        var safeName = string.IsNullOrWhiteSpace(data.PersonalInfo?.FullName) 
            ? "Resume" 
            : data.PersonalInfo.FullName.Replace(" ", "_");

        var fileName = $"{safeName}_Resume.pdf";
        return (stream, fileName);
    }

    private static ResumeDataDto ParseResumeData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
        {
            return new ResumeDataDto();
        }

        try
        {
            return JsonSerializer.Deserialize<ResumeDataDto>(json, JsonOptions) ?? new ResumeDataDto();
        }
        catch
        {
            return new ResumeDataDto();
        }
    }
}
