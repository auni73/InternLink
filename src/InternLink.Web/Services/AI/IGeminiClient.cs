using InternLink.Web.Models.Enums;

namespace InternLink.Web.Services.AI;

public interface IGeminiClient
{
    /// <summary>
    /// Generates content and records the call in the AIHistory token ledger.
    /// </summary>
    /// <param name="jsonMode">Sets responseMimeType=application/json so structured-output callers avoid malformed-JSON retries.</param>
    /// <exception cref="AiServiceException">Every failure mode, including exhausted keys and transport errors.</exception>
    Task<GeminiResponse> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        IntegrationFeature feature,
        Guid userId,
        bool jsonMode,
        CancellationToken ct = default);
}
