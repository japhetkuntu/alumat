namespace ReservEase.Alumni.Member.Api.Models;

public record RsvpRequest(string EventId);

/// <summary>Where Paystack should send the member after paying for a ticket.</summary>
public record EventTicketBody(string? CallbackUrl);
