using InternLink.Web.Models.Enums;

namespace InternLink.Web.Services.AI;

/// <summary>One turn of a conversation. <paramref name="IsUser"/> false marks a prior model reply.</summary>
public sealed record ChatMessage(bool IsUser, string Text);

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

    /// <summary>
    /// Sends a full conversation as native multi-turn contents so the model keeps track of who said what.
    /// Flattening history into one prompt loses that distinction and invites the model to break character.
    /// </summary>
    /// <exception cref="AiServiceException">Every failure mode, including exhausted keys and transport errors.</exception>
    Task<GeminiResponse> GenerateChatAsync(
        string systemPrompt,
        IReadOnlyList<ChatMessage> history,
        IntegrationFeature feature,
        Guid userId,
        CancellationToken ct = default);
}
