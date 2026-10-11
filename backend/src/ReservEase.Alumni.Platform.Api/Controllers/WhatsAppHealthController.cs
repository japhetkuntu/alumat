using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace ReservEase.Alumni.Platform.Api.Controllers;

[Authorize(Roles = "SuperAdmin,Support")]
[Route("api/v{version:apiVersion}/whatsapp-health")]
public class WhatsAppHealthController(IWhatsAppHealthService health) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "WhatsApp channel health", Description = "Configuration and connection state, and adoption counts. No numbers, members or secrets.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<WhatsAppHealthDto>))]
    public async Task<IActionResult> Get() => (await health.GetAsync()).ToActionResult();
}
