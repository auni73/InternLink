namespace InternLink.Web.Services.Vectors;

/// <summary>Everything needed to build a job's embedding document and Qdrant payload.</summary>
public sealed class JobVectorSource
{
    public Guid JobId { get; init; }
    public Guid CompanyId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string CoreDescription { get; init; } = string.Empty;
    public string SelectionCriteria { get; init; } = string.Empty;
    public int LocationType { get; init; }
    public DateTimeOffset DeadLine { get; init; }

    /// <summary>Skills ordered by required importance weight, descending.</summary>
    public IReadOnlyList<Guid> SkillIds { get; init; } = [];

    public IReadOnlyList<string> SkillNames { get; init; } = [];

    public string ToDocumentText() =>
        $"{Title}\n{CoreDescription}\n{SelectionCriteria}\nSkills: {string.Join(", ", SkillNames)}";

    public JobVectorPayload ToPayload() =>
        new(CompanyId, LocationType, DeadLine.ToUnixTimeSeconds(), SkillIds);
}
