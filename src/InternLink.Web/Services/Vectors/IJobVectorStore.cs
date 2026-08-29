namespace InternLink.Web.Services.Vectors;

/// <summary>Write side of the job vector index, used by the background indexer.</summary>
public interface IJobVectorStore
{
    Task EnsureCollectionAsync(CancellationToken ct = default);

    Task UpsertJobAsync(Guid jobId, float[] vector, JobVectorPayload payload, CancellationToken ct = default);

    Task DeleteJobAsync(Guid jobId, CancellationToken ct = default);
}

/// <summary>Read side of the job vector index.</summary>
public interface IVectorSearch
{
    /// <summary>
    /// Returns the closest job ids, filtered server-side to postings whose deadline has not passed.
    /// </summary>
    /// <exception cref="InternLink.Web.Services.AI.SemanticSearchUnavailableException">Vector store unavailable.</exception>
    Task<IReadOnlyList<(Guid JobId, float Score)>> SearchJobsAsync(
        float[] queryVector,
        int topK,
        CancellationToken ct = default);
}
