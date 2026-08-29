using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Assessment;

public interface IAssessmentSessionService
{
    string CreateSessionToken(Guid studentId, Guid skillId, string skillName, IEnumerable<string> questionIds);
    (bool IsValid, AssessmentSessionPayload? Payload, string? ErrorMessage) ValidateSessionToken(string token, Guid expectedStudentId, Guid expectedSkillId);
}
