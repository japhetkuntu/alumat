using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface ISupportTicketService
{
    Task<IApiResponse<List<SupportTicketResponse>>> GetTicketsAsync(AuthData admin);
    Task<IApiResponse<SupportTicketResponse>> CreateTicketAsync(CreateSupportTicketRequest request, AuthData admin);
}
