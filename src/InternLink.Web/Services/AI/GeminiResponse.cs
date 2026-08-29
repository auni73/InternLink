namespace InternLink.Web.Services.AI;

/// <summary>
/// Structured result: the ledger needs the real usageMetadata counts, and callers parse <see cref="Content"/> themselves.
/// </summary>
public sealed record GeminiResponse(
    string Content,
    int PromptTokens,
    int CompletionTokens,
    decimal EstimatedCostUsd);
