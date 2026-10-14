using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace ReservEase.Alumni.Platform.Api.Controllers;

public record SetInstitutionWhatsAppRequest(bool Enabled);

[Authorize(Roles = "SuperAdmin,Support")]
[Route("api/v{version:apiVersion}/whatsapp-health")]
public class WhatsAppHealthController(IWhatsAppHealthService health) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "WhatsApp channel health", Description = "Configuration and connection state, and adoption counts. No numbers, members or secrets.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<WhatsAppHealthDto>))]
    public async Task<IActionResult> Get() => (await health.GetAsync()).ToActionResult();

    [HttpGet("institutions")]
    [SwaggerOperation(Summary = "Institutions and their WhatsApp switch", Description = "Every active institution with whether WhatsApp is activated for it, and whether it also opted out itself")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<WhatsAppInstitutionRow>>))]
    public async Task<IActionResult> Institutions() => (await health.ListInstitutionsAsync()).ToActionResult();

    [Authorize(Roles = "SuperAdmin")]
    [HttpPut("institutions/{id}")]
    [SwaggerOperation(Summary = "Activate or deactivate WhatsApp for one institution", Description = "Deactivating blocks the bot and WhatsApp notifications for that institution only; its notifications use SMS and email. Needs the platform-wide switch on to have any effect for activation.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<WhatsAppInstitutionRow>))]
    public async Task<IActionResult> SetInstitution(string id, [FromBody] SetInstitutionWhatsAppRequest request)
    {
        var admin = User.GetAccount();
        return (await health.SetInstitutionAsync(id, request.Enabled, admin.Id, $"{admin.FirstName} {admin.LastName}".Trim())).ToActionResult();
    }
}
