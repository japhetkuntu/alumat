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
}
