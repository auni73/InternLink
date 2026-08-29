namespace InternLink.Web.Services.AI;

/// <summary>
/// Retrieval task type. Google's embedding models place documents and queries in an
/// asymmetric space, so indexing and searching must declare different purposes.
/// </summary>
public enum EmbeddingPurpose
{
    /// <summary>Indexing a job posting (maps to RETRIEVAL_DOCUMENT).</summary>
    Document = 0,

    /// <summary>Embedding a student profile or search query (maps to RETRIEVAL_QUERY).</summary>
    Query = 1
}
