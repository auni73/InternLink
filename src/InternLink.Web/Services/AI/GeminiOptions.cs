namespace InternLink.Web.Services.AI;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>Comma-separated rotation pool. Real values live in user-secrets, never in appsettings.json.</summary>
    public string ApiKeys { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-2.5-flash";

    public string EmbeddingModel { get; set; } = "text-embedding-004";

    public string BaseAddress { get; set; } = "https://generativelanguage.googleapis.com/";

    public int TimeoutSeconds { get; set; } = 30;

    public int KeyCooldownSeconds { get; set; } = 60;
}
