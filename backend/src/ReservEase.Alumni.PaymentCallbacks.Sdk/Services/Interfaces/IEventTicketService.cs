using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Models;

namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Interfaces;

/// <summary>Paying for a seat at a paid event. A ticket is final: it is never cancelled by the member and never refunded.</summary>
public interface IEventTicketService
{
    Task<IApiResponse<EventTicketCheckoutResponse>> InitiateAsync(EventTicketRequest request, AuthData member);
    Task<IApiResponse<EventTicketStatusResponse>> GetStatusAsync(string reference, AuthData member);
    /// <summary>Cheap existence check the Paystack webhook dispatcher uses to decide whether a reference belongs to a ticket.</summary>
    Task<bool> OwnsReferenceAsync(string reference);
}
