namespace InternLink.Web.Services.AI;

/// <summary>
/// Thrown when embeddings or the vector store are unavailable. Callers catch exactly this
/// to fall back to SQL Server full-text search.
/// </summary>
public class SemanticSearchUnavailableException : Exception
{
    public SemanticSearchUnavailableException(string message) : base(message)
    {
    }

    public SemanticSearchUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
