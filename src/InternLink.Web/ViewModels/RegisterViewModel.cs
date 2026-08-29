using System.ComponentModel.DataAnnotations;

namespace InternLink.Web.ViewModels;

public enum RegistrationRole
{
    Student = 0,
    Company = 1
}

// Conditional fields validated via IValidatableObject so server-side rules stay authoritative.
public class RegisterViewModel : IValidatableObject
{
    [Required]
    public RegistrationRole Role { get; set; } = RegistrationRole.Student;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    // --- Student fields (mirror dbo.Students constraints) ---
    [StringLength(100)]
    [Display(Name = "First name")]
    public string? FirstName { get; set; }

    [StringLength(100)]
    [Display(Name = "Last name")]
    public string? LastName { get; set; }

    [StringLength(50)]
    [Display(Name = "Institutional ID")]
    public string? InstitutionalId { get; set; }

    [StringLength(100)]
    public string? Department { get; set; }

    [Display(Name = "CGPA")]
    public decimal? CGPA { get; set; }

    // --- Company fields (mirror dbo.Companies constraints) ---
    [StringLength(200)]
    [Display(Name = "Company name")]
    public string? CompanyName { get; set; }

    [StringLength(100)]
    [Display(Name = "Industry sector")]
    public string? IndustrySector { get; set; }

    [StringLength(500)]
    [Url]
    [Display(Name = "Corporate website")]
    public string? CorporateWebsite { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Role == RegistrationRole.Student)
        {
            if (string.IsNullOrWhiteSpace(FirstName))
                yield return new ValidationResult("First name is required.", new[] { nameof(FirstName) });
            if (string.IsNullOrWhiteSpace(LastName))
                yield return new ValidationResult("Last name is required.", new[] { nameof(LastName) });
            if (string.IsNullOrWhiteSpace(InstitutionalId))
                yield return new ValidationResult("Institutional ID is required.", new[] { nameof(InstitutionalId) });
            if (string.IsNullOrWhiteSpace(Department))
                yield return new ValidationResult("Department is required.", new[] { nameof(Department) });
            if (CGPA is null)
                yield return new ValidationResult("CGPA is required.", new[] { nameof(CGPA) });
            else if (CGPA < 0.00m || CGPA > 4.00m)
                yield return new ValidationResult("CGPA must be between 0.00 and 4.00.", new[] { nameof(CGPA) });
        }
        else
        {
            if (string.IsNullOrWhiteSpace(CompanyName))
                yield return new ValidationResult("Company name is required.", new[] { nameof(CompanyName) });
            if (string.IsNullOrWhiteSpace(IndustrySector))
                yield return new ValidationResult("Industry sector is required.", new[] { nameof(IndustrySector) });
        }
    }
}
