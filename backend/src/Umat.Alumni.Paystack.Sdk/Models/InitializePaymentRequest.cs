namespace Umat.Alumni.Paystack.Sdk.Models;

public class InitializePaymentRequest
{
    public string Email { get; set; } = string.Empty;
    public long Amount { get; set; } // in kobo (pesewas for GHS)
    public string Reference { get; set; } = Guid.NewGuid().ToString("N");
    public string? CallbackUrl { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
