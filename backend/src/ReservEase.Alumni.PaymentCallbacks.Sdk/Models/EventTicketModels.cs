namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Models;

public class EventTicketRequest
{
    public string EventId { get; set; } = "";
    /// <summary>Which RSVP price the member chose. May be left out when the event has just one price on sale.</summary>
    public string? OptionId { get; set; }
    /// <summary>Where Paystack sends the member after paying. The member portal passes its own ticket-status page.</summary>
    public string? CallbackUrl { get; set; }
}

public class EventTicketCheckoutResponse
{
    public string? AuthorizationUrl { get; set; }
    public string Reference { get; set; } = "";
}

public class EventTicketStatusResponse
{
    public string Reference { get; set; } = "";
    public string EventId { get; set; } = "";
    public string? EventTitle { get; set; }
    /// <summary>Pending, Successful or Failed, the same words the store uses.</summary>
    public string Status { get; set; } = "";
    /// <summary>The ticket price. Never includes any fee.</summary>
    public decimal? Amount { get; set; }
    /// <summary>The RSVP price option bought, as it was named then.</summary>
    public string? OptionName { get; set; }
    public string Message { get; set; } = "";
}
