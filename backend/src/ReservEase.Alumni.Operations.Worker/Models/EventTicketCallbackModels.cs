namespace ReservEase.Alumni.Operations.Worker.Models;

public sealed class EventTicketPaystackVerifyResult
{
    public bool Status { get; init; }
    public string? Message { get; init; }
    public string PaystackStatus { get; init; } = "unknown";
    public decimal GrossAmount { get; init; }
    public decimal? GatewayFee { get; init; }
    public string? GatewayResponse { get; init; }
}

public sealed class EventSeatSync
{
    public int Count { get; init; }
    public int? Capacity { get; init; }
    public string? EventTitle { get; init; }
}
