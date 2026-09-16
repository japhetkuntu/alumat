namespace ReservEase.Alumni.Operations.Worker.Models;

public sealed class StoreOrderPaystackVerifyResult
{
    public bool Status { get; init; }
    public string? Message { get; init; }
    public string PaystackStatus { get; init; } = "unknown";
    public decimal GrossAmount { get; init; }
    public decimal? GatewayFee { get; init; }
    public string? GatewayResponse { get; init; }
}
