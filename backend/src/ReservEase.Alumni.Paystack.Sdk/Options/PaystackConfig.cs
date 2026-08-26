namespace ReservEase.Alumni.Paystack.Sdk.Options;

public class PaystackConfig
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.paystack.co";
    public string CallbackUrl { get; set; } = string.Empty;

    // ── Zero-Deduction gross-up inputs ──────────────────────────────────────
    // Paystack's own processing fee, used to gross up the customer-facing
    // charge so it's fully absorbed and never deducted from the institution
    // or the platform. These must track Paystack's actual published GHS fee
    // schedule — verify against https://paystack.com/gh/pricing before
    // changing, and periodically re-confirm, since gateways revise pricing.
    /// <summary>Paystack's percentage processing fee, e.g. 1.95 = 1.95%.</summary>
    public decimal GatewayFeePercentage { get; set; } = 1.95m;
    /// <summary>Any flat fee component Paystack adds on top of the percentage, in pesewas. 0 if none.</summary>
    public long GatewayFixedFeeSubunit { get; set; } = 0;
    /// <summary>Paystack's fee cap for local GHS transactions, in pesewas, if one applies. Null = uncapped.</summary>
    public long? GatewayFeeCapSubunit { get; set; }
    /// <summary>
    /// Extra pesewas padded onto every charge, on top of the calculated
    /// gross-up, so the platform's fee is a hard guarantee rather than an
    /// estimate — absorbs rounding drift between our fee estimate and
    /// Paystack's real fee at settlement. Charged to the payer, never
    /// deducted from the institution or the platform's own cut.
    /// </summary>
    public long GatewayFeeSafetyBufferSubunit { get; set; } = 2;
}
