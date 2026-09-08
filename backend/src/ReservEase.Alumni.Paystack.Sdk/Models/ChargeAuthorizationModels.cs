namespace ReservEase.Alumni.Paystack.Sdk.Models;

/// <summary>Re-charges a previously saved, reusable authorization off-session — the recurring-giving mechanism, using the same Zero-Deduction split fields as a normal InitializePaymentRequest.</summary>
public class ChargeAuthorizationRequest
{
    public string AuthorizationCode { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Reference { get; set; } = Guid.NewGuid().ToString("N");
    public Dictionary<string, string>? Metadata { get; set; }
    public string? Subaccount { get; set; }
    public long? TransactionCharge { get; set; }
    public string? Bearer { get; set; }
}

/// <summary>Same response shape as verifying a transaction — Paystack returns the charge's outcome synchronously, no webhook round-trip needed.</summary>
public class ChargeAuthorizationResponse
{
    public bool Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public VerifyPaymentData? Data { get; set; }
}
