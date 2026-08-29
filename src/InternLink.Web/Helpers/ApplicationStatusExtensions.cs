using InternLink.Web.Models.Enums;

namespace InternLink.Web.Helpers;

public static class ApplicationStatusExtensions
{
    public static string GetDisplayName(this ApplicationStatus status) => status switch
    {
        ApplicationStatus.Applied => "Applied",
        ApplicationStatus.Screened => "Under Review / Screened",
        ApplicationStatus.Scheduled => "Interview Scheduled",
        ApplicationStatus.Offered => "Offer Extended",
        ApplicationStatus.Rejected => "Not Selected",
        _ => status.ToString()
    };

    public static string GetShortLabel(this ApplicationStatus status) => status switch
    {
        ApplicationStatus.Applied => "Applied",
        ApplicationStatus.Screened => "Screened",
        ApplicationStatus.Scheduled => "Scheduled",
        ApplicationStatus.Offered => "Offered",
        ApplicationStatus.Rejected => "Rejected",
        _ => status.ToString()
    };

    public static string GetBadgeClass(this ApplicationStatus status) => status switch
    {
        ApplicationStatus.Applied => "bg-secondary-subtle text-secondary-emphasis border border-secondary-subtle",
        ApplicationStatus.Screened => "bg-primary-subtle text-primary border border-primary-subtle",
        ApplicationStatus.Scheduled => "bg-warning-subtle text-warning-emphasis border border-warning-subtle",
        ApplicationStatus.Offered => "bg-success-subtle text-success border border-success-subtle",
        ApplicationStatus.Rejected => "bg-danger-subtle text-danger border border-danger-subtle",
        _ => "bg-light text-dark"
    };

    public static string GetBadgeHex(this ApplicationStatus status) => status switch
    {
        ApplicationStatus.Applied => "#64748B",   // Slate 500
        ApplicationStatus.Screened => "#0F6B5C",  // Primary Teal
        ApplicationStatus.Scheduled => "#F2A33C", // Amber Accent
        ApplicationStatus.Offered => "#10B981",   // Emerald Green
        ApplicationStatus.Rejected => "#EF4444",  // Red
        _ => "#64748B"
    };

    public static int GetStepIndex(this ApplicationStatus status) => status switch
    {
        ApplicationStatus.Applied => 0,
        ApplicationStatus.Screened => 1,
        ApplicationStatus.Scheduled => 2,
        ApplicationStatus.Offered => 3,
        ApplicationStatus.Rejected => 3,
        _ => 0
    };
}
