namespace InternLink.Web.Models.Enums;

public enum JobSource : byte
{
    Internal = 0, // Direct employer posting (in-app ATS apply flow)
    External = 1  // Aggregated third-party posting (redirect to external source portal)
}
