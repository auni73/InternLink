namespace InternLink.Web.Services.AI;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>Comma-separated rotation pool. Real values live in user-secrets, never in appsettings.json.</summary>
    public string ApiKeys { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-3.6-flash";

    public string EmbeddingModel { get; set; } = "gemini-embedding-2";

    public string BaseAddress { get; set; } = "https://generativelanguage.googleapis.com/";

    public int TimeoutSeconds { get; set; } = 30;

    public int KeyCooldownSeconds { get; set; } = 60;

    /// <summary>Base delay for the jittered exponential retry backoff. Tests set this to 0.</summary>
    public int RetryBaseDelayMilliseconds { get; set; } = 1000;
}
