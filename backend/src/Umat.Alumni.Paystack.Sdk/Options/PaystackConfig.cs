namespace Umat.Alumni.Paystack.Sdk.Options;

public class PaystackConfig
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.paystack.co";
    public string CallbackUrl { get; set; } = string.Empty;
}
