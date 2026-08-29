using System.Text;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Services.AI;
using InternLink.Web.Services.Resume;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.CoverLetter;

public class CoverLetterService : ICoverLetterService
{
    private readonly IStudentRepository _students;
    private readonly IJobRepository _jobs;
    private readonly IResumeService _resumes;
    private readonly IApplicationRepository _applications;
    private readonly IGeminiClient _gemini;
    private readonly ILogger<CoverLetterService> _logger;

    public CoverLetterService(
        IStudentRepository students,
        IJobRepository jobs,
        IResumeService resumes,
        IApplicationRepository applications,
        IGeminiClient gemini,
        ILogger<CoverLetterService> logger)
    {
        _students = students;
        _jobs = jobs;
        _resumes = resumes;
        _applications = applications;
        _gemini = gemini;
        _logger = logger;
    }

    public async Task<string?> GenerateAsync(Guid studentId, Guid jobId, CancellationToken ct = default)
    {
        var student = await _students.GetByIdAsync(studentId, ct);
        if (student is null)
        {
            return null;
        }

        var job = await _jobs.GetApprovedJobDetailAsync(jobId, studentId, ct);
        if (job is null)
        {
            return null;
        }

        var skills = await _students.GetStudentSkillsAsync(studentId, ct);
        var resumeExcerpt = await BuildResumeExcerptAsync(studentId, ct);

        // The PDF renderer already supplies the salutation and sign-off, so the model must not repeat them.
        const string systemPrompt =
            "You write cover letter body prose for a university student applying to an internship. " +
            "Write 250 to 400 words of plain prose in two to four paragraphs. " +
            "Do not include a salutation, a sign-off, a date, an address block, markdown, or bullet points. " +
            "Be specific: name the company, the role, and the student's actual skills and projects. " +
            "Never invent employers, degrees, or achievements that are not supplied.";

        var userPrompt = new StringBuilder()
            .AppendLine($"Student: {student.FirstName} {student.LastName}")
            .AppendLine($"Department: {student.Department}")
            .AppendLine($"Interests: {student.Interests}")
            .AppendLine($"Biography: {student.Biography}")
            .AppendLine($"Skills: {string.Join(", ", skills.Select(s => s.Skill.SkillName))}")
            .AppendLine()
            .AppendLine("Resume highlights:")
            .AppendLine(resumeExcerpt)
            .AppendLine()
            .AppendLine($"Company: {job.CompanyName}")
            .AppendLine($"Role: {job.Title}")
            .AppendLine($"Description: {job.CoreDescription}")
            .AppendLine($"Selection criteria: {job.SelectionCriteria}")
            .ToString();

        try
        {
            // jsonMode is deliberately off: free prose is the product here.
            var response = await _gemini.GenerateAsync(
                systemPrompt,
                userPrompt,
                IntegrationFeature.CoverLetter,
                student.UserId,
                jsonMode: false,
                ct);

            return response.Content.Trim();
        }
        catch (AiServiceException ex)
        {
            _logger.LogWarning(ex, "Cover letter generation unavailable for student {StudentId}.", studentId);
            throw;
        }
    }

    public async Task<bool> SaveToApplicationAsync(
        Guid studentId,
        Guid jobId,
        string finalText,
        CancellationToken ct = default)
    {
        // Requiring an existing application keeps the contract honest: nothing is stored in limbo,
        // and the student is never told their letter was attached to something that does not exist.
        return await _applications.UpdateCoverLetterAsync(jobId, studentId, finalText, ct);
    }

    private async Task<string> BuildResumeExcerptAsync(Guid studentId, CancellationToken ct)
    {
        var resumes = await _resumes.GetStudentResumesAsync(studentId, ct);
        var latest = resumes
            .Where(r => r.IsFinalized)
            .OrderByDescending(r => r.LastModified)
            .FirstOrDefault()
            ?? resumes.OrderByDescending(r => r.LastModified).FirstOrDefault();

        if (latest is null)
        {
            return "No resume on file.";
        }

        var detail = await _resumes.GetResumeForEditAsync(latest.Id, studentId, ct);
        if (detail is null)
        {
            return "No resume on file.";
        }

        var data = detail.Data;
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(data.PersonalInfo.Summary))
        {
            builder.AppendLine($"Summary: {data.PersonalInfo.Summary}");
        }

        foreach (var e in data.Experience)
        {
            builder.AppendLine($"- {e.Role} at {e.Company}: {e.Description} {e.Highlights}");
        }

        foreach (var p in data.Projects)
        {
            builder.AppendLine($"- Project {p.Title} [{p.TechStack}]: {p.Description}");
        }

        foreach (var edu in data.Education)
        {
            builder.AppendLine($"- {edu.Degree} in {edu.FieldOfStudy}, {edu.Institution}");
        }

        return builder.Length > 0 ? builder.ToString() : "No resume on file.";
    }
}
