namespace InternLink.Web.Services.Vectors;

/// <summary>Qdrant payload for a job point. Kept flat so it can be filtered server-side.</summary>
public sealed record JobVectorPayload(
    Guid CompanyId,
    int LocationType,
    long DeadlineUnix,
    IReadOnlyList<Guid> SkillIds);
