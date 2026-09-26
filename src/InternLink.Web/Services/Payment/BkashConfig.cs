namespace InternLink.Web.Services.Payment;

public class BkashConfig
{
    public const string SectionName = "Bkash";

    public string BaseUrl { get; set; } = "https://tokenized.sandbox.bka.sh/v1.2.0-beta/tokenized";
    public string AppKey { get; set; } = "4f6o0cjiki2rfm34kfdadl1eqq";
    public string AppSecret { get; set; } = "2is7hdktrekvrbljjh44ll3d9l1dtjo4pasmjvs5vl5qr3fug4b";
    public string Username { get; set; } = "sandboxTokenizedUser02";
    public string Password { get; set; } = "sandboxTokenizedUser02@12345";
    public string CallbackUrl { get; set; } = "/Company/Subscription/BkashCallback";
    public bool UseSimulatedGateway { get; set; } = false;
}
