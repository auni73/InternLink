namespace InternLink.Web.Services.AI;

/// <summary>
/// Gemini 2.5 Flash list pricing in USD per 1M tokens. Provider pricing changes — re-check
/// https://ai.google.dev/gemini-api/docs/pricing periodically and update these constants.
/// </summary>
public static class GeminiPricing
{
    public const decimal InputUsdPerMillionTokens = 0.30m;
    public const decimal OutputUsdPerMillionTokens = 2.50m;

    public static decimal Estimate(int promptTokens, int completionTokens)
    {
        var input = promptTokens * InputUsdPerMillionTokens;
        var output = completionTokens * OutputUsdPerMillionTokens;
        return decimal.Round((input + output) / 1_000_000m, 4, MidpointRounding.AwayFromZero);
    }
}
