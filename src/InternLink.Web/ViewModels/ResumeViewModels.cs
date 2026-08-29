using System.ComponentModel.DataAnnotations;
using InternLink.Web.Models;

namespace InternLink.Web.ViewModels;

public class PersonalInfoStepDto
{
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Location { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? LinkedIn { get; set; }

    [MaxLength(200)]
    public string? GitHub { get; set; }

    [MaxLength(200)]
    public string? Portfolio { get; set; }

    [MaxLength(2000)]
    public string? Summary { get; set; }
}

public class EducationEntryDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Institution { get; set; } = string.Empty;
    public string Degree { get; set; } = string.Empty;
    public string FieldOfStudy { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public string? Gpa { get; set; }
    public string? Highlights { get; set; }
}

public class ExperienceEntryDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Company { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Highlights { get; set; }
}

public class SkillEntryDto
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public int ProficiencyLevel { get; set; } = 3; // 1 - 5
    public string? Domain { get; set; }
}

public class ProjectEntryDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? TechStack { get; set; }
    public string? Link { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class ResumeDataDto
{
    public PersonalInfoStepDto PersonalInfo { get; set; } = new();
    public List<EducationEntryDto> Education { get; set; } = new();
    public List<ExperienceEntryDto> Experience { get; set; } = new();
    public List<SkillEntryDto> Skills { get; set; } = new();
    public List<ProjectEntryDto> Projects { get; set; } = new();
}

public class ResumeItemViewModel
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string? DocumentPath { get; set; }
    public bool IsFinalized => !string.IsNullOrWhiteSpace(DocumentPath);
    public DateTimeOffset LastModified { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int SkillCount { get; set; }
    public int ExperienceCount { get; set; }
}

public class ResumeListViewModel
{
    public IReadOnlyList<ResumeItemViewModel> Resumes { get; set; } = new List<ResumeItemViewModel>();
}

public class ResumeBuilderViewModel
{
    public Guid ResumeId { get; set; }
    public Guid StudentId { get; set; }
    public bool IsFinalized { get; set; }
    public ResumeDataDto Data { get; set; } = new();
    public IReadOnlyList<Skill> AvailableSkills { get; set; } = new List<Skill>();
    public string CurrentStep { get; set; } = "personal-info";
}

public class StudentProfileViewModel
{
    public Guid StudentId { get; set; }

    public string InstitutionalId { get; set; } = string.Empty;

    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(100, ErrorMessage = "First name cannot exceed 100 characters.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [MaxLength(100, ErrorMessage = "Last name cannot exceed 100 characters.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "CGPA is required.")]
    [Range(0.00, 4.00, ErrorMessage = "CGPA must be between 0.00 and 4.00.")]
    public decimal CGPA { get; set; }

    [Required(ErrorMessage = "Department is required.")]
    [MaxLength(100, ErrorMessage = "Department cannot exceed 100 characters.")]
    public string Department { get; set; } = string.Empty;

    [MaxLength(2000, ErrorMessage = "Biography cannot exceed 2000 characters.")]
    public string? Biography { get; set; }

    [MaxLength(500, ErrorMessage = "Interests cannot exceed 500 characters.")]
    public string? Interests { get; set; }

    public IReadOnlyList<StudentSkill> CurrentSkills { get; set; } = new List<StudentSkill>();
    public IReadOnlyList<Guid> VerifiedSkillIds { get; set; } = new List<Guid>();
}
