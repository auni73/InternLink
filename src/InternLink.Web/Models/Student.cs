namespace InternLink.Web.Models;

public class Student
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public decimal CGPA { get; set; }
    public string InstitutionalId { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string? Biography { get; set; }
    public string? Interests { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual AppUser User { get; set; } = null!;
    public virtual ICollection<Resume> Resumes { get; set; } = new List<Resume>();
    public virtual ICollection<Application> Applications { get; set; } = new List<Application>();
    public virtual ICollection<StudentSkill> StudentSkills { get; set; } = new List<StudentSkill>();
    public virtual ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();
    public virtual ICollection<CounselorFeedback> CounselorFeedbacks { get; set; } = new List<CounselorFeedback>();
}
