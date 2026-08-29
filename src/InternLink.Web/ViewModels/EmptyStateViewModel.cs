namespace InternLink.Web.ViewModels;

public class EmptyStateViewModel
{
    public string Icon { get; set; } = "bi-inbox";
    public string Title { get; set; } = "Nothing here yet";
    public string? Message { get; set; }
    public string? CtaText { get; set; }
    public string? CtaUrl { get; set; }
}
