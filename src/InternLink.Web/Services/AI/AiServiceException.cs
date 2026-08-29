namespace InternLink.Web.Services.AI;

/// <summary>
/// Thrown for every AI-gateway failure so callers never have to catch transport-level exceptions.
/// </summary>
public class AiServiceException : Exception
{
    public AiServiceException(string message) : base(message)
    {
    }

    public AiServiceException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
