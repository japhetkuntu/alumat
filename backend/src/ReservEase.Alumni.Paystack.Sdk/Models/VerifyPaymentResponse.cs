namespace ReservEase.Alumni.Paystack.Sdk.Models;

public class VerifyPaymentResponse
{
    public bool Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public VerifyPaymentData? Data { get; set; }
}

public class VerifyPaymentData
{
    public string Status { get; set; } = string.Empty; // success, failed, pending
    public string Reference { get; set; } = string.Empty;
    public long Amount { get; set; }
    /// <summary>Paystack's actual processing fee for this transaction, in subunit — null if Paystack didn't report one (older API responses, some channels).</summary>
    public long? Fees { get; set; }
    public string GatewayResponse { get; set; } = string.Empty;
    public string PaidAt { get; set; } = string.Empty;

    /// <summary>Present on a successful card charge — the reusable token behind recurring giving's charge_authorization calls. Null for channels that don't support reuse (e.g. most mobile money).</summary>
    public PaystackAuthorization? Authorization { get; set; }
}

/// <summary>The card/channel Paystack charged, and — when <see cref="Reusable"/> — the token that can be re-charged later via /transaction/charge_authorization without the customer present.</summary>
public class PaystackAuthorization
{
    public string AuthorizationCode { get; set; } = string.Empty;
    public bool Reusable { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string? Last4 { get; set; }
    public string? CardType { get; set; }
    public string? Bank { get; set; }
    public string? ExpMonth { get; set; }
    public string? ExpYear { get; set; }
}
