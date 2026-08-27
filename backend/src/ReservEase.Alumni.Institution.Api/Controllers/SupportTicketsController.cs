using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;

namespace ReservEase.Alumni.Institution.Api.Controllers;

[Authorize]
[Route("api/v{version:apiVersion}/support-tickets")]
public class SupportTicketsController(ISupportTicketService supportTicketService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List this institution's own support tickets")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<SupportTicketResponse>>))]
    public async Task<IActionResult> GetTickets()
    {
        var result = await supportTicketService.GetTicketsAsync(User.GetAccount());
        return result.ToActionResult();
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Submit a support ticket to the platform team")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<SupportTicketResponse>))]
    public async Task<IActionResult> CreateTicket([FromBody] CreateSupportTicketRequest request)
    {
        var result = await supportTicketService.CreateTicketAsync(request, User.GetAccount());
        return result.ToActionResult();
    }
}
