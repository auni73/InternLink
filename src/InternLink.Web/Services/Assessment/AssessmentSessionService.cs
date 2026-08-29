using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Assessment;

public class AssessmentSessionService : IAssessmentSessionService
{
    private const string Purpose = "InternLink.Assessments.SessionToken.v1";
    private readonly IDataProtector _protector;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AssessmentSessionService> _logger;

    public static readonly TimeSpan AllowedDuration = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan TotalAllowedTime = AllowedDuration + GracePeriod; // 10 min 15 sec (615s)

    public AssessmentSessionService(
        IDataProtectionProvider dataProtectionProvider,
        TimeProvider? timeProvider,
        ILogger<AssessmentSessionService> logger)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger;
    }

    public string CreateSessionToken(Guid studentId, Guid skillId, string skillName, IEnumerable<string> questionIds)
    {
        var payload = new AssessmentSessionPayload
        {
            StudentId = studentId,
            SkillId = skillId,
            SkillName = skillName,
            QuestionIds = questionIds?.ToList() ?? new List<string>(),
            IssuedAtUtc = _timeProvider.GetUtcNow()
        };

        var json = JsonSerializer.Serialize(payload);
        return _protector.Protect(json);
    }

    public (bool IsValid, AssessmentSessionPayload? Payload, string? ErrorMessage) ValidateSessionToken(
        string token, 
        Guid expectedStudentId, 
        Guid expectedSkillId)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, null, "Session token is required.");
        }

        try
        {
            var unprotectedJson = _protector.Unprotect(token);
            var payload = JsonSerializer.Deserialize<AssessmentSessionPayload>(unprotectedJson);

            if (payload == null)
            {
                return (false, null, "Malformed session token.");
            }

            if (payload.StudentId != expectedStudentId)
            {
                _logger.LogWarning("Session token student mismatch. Expected {Expected}, Token has {Actual}", expectedStudentId, payload.StudentId);
                return (false, null, "Session does not belong to the current student.");
            }

            if (payload.SkillId != expectedSkillId)
            {
                _logger.LogWarning("Session token skill mismatch. Expected {Expected}, Token has {Actual}", expectedSkillId, payload.SkillId);
                return (false, null, "Session token does not match the assessment skill.");
            }

            var now = _timeProvider.GetUtcNow();
            var elapsed = now - payload.IssuedAtUtc;

            if (elapsed > TotalAllowedTime)
            {
                _logger.LogWarning("Assessment submission timed out. Elapsed: {Elapsed}s, Limit: {Limit}s", elapsed.TotalSeconds, TotalAllowedTime.TotalSeconds);
                return (false, null, $"Assessment time limit exceeded ({Math.Round(elapsed.TotalMinutes, 1)} minutes elapsed). Submissions must be completed within 10 minutes.");
            }

            return (true, payload, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to unprotect assessment session token.");
            return (false, null, "Invalid or expired assessment session token.");
        }
    }
}
