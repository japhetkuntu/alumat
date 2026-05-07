namespace Umat.Alumni.Paystack.Sdk.Models;

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
    public string GatewayResponse { get; set; } = string.Empty;
    public string PaidAt { get; set; } = string.Empty;
}
