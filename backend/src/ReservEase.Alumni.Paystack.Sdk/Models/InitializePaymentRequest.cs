namespace ReservEase.Alumni.Paystack.Sdk.Models;

public class InitializePaymentRequest
{
    public string Email { get; set; } = string.Empty;
    public long Amount { get; set; } // in kobo (pesewas for GHS)
    public string Reference { get; set; } = Guid.NewGuid().ToString("N");
    public string? CallbackUrl { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// The institution's Paystack subaccount code. When set, Paystack splits
    /// this payment automatically per the subaccount's own percentage_charge
    /// (the platform's cut) — no per-transaction override needed.
    /// </summary>
    public string? Subaccount { get; set; }
}
