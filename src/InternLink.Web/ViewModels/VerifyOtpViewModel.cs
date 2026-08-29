using System.ComponentModel.DataAnnotations;

namespace InternLink.Web.ViewModels;

public class VerifyOtpViewModel
{
    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Enter the 6-digit code.")]
    [Display(Name = "Verification code")]
    public string Code { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
