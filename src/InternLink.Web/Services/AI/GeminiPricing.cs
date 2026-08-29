namespace InternLink.Web.Services.AI;

/// <summary>
/// Gemini 3.6 Flash paid-tier list pricing in USD per 1M tokens. Free-tier keys bill at zero,
/// so this is an upper bound. Rates double to $1.50 / $7.50 on 2027-01-01 — re-check
/// https://ai.google.dev/gemini-api/docs/pricing and update these constants then.
/// </summary>
public static class GeminiPricing
{
    public const decimal InputUsdPerMillionTokens = 0.75m;
    public const decimal OutputUsdPerMillionTokens = 3.75m;

    public static decimal Estimate(int promptTokens, int completionTokens)
    {
        var input = promptTokens * InputUsdPerMillionTokens;
        var output = completionTokens * OutputUsdPerMillionTokens;
        return decimal.Round((input + output) / 1_000_000m, 4, MidpointRounding.AwayFromZero);
    }
}
