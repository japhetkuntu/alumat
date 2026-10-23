using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>Partner integration for one institution: its settings and API keys. Platform SuperAdmins only.</summary>
[Authorize(Roles = "SuperAdmin")]
[Route("api/v{version:apiVersion}/institution-integrations/{institutionId}")]
public class InstitutionIntegrationsController(IInstitutionIntegrationService integrationService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Get an institution's integration settings and keys")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IntegrationSettingsResponse>))]
    public async Task<IActionResult> Get(string institutionId) => (await integrationService.GetAsync(institutionId)).ToActionResult();

    [HttpPut]
    [SwaggerOperation(Summary = "Save an institution's integration settings")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IntegrationSettingsResponse>))]
    public async Task<IActionResult> Update(string institutionId, [FromBody] UpdateIntegrationRequest request)
    {
        var acct = User.GetAccount();
        return (await integrationService.UpdateAsync(institutionId, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim())).ToActionResult();
    }

    [HttpPost("keys")]
    [SwaggerOperation(Summary = "Create an API key (the key is shown once)")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<CreatedIntegrationKeyResponse>))]
    public async Task<IActionResult> CreateKey(string institutionId, [FromBody] CreateIntegrationKeyRequest request)
    {
        var acct = User.GetAccount();
        return (await integrationService.CreateKeyAsync(institutionId, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim())).ToActionResult();
    }

    [HttpDelete("keys/{keyId}")]
    [SwaggerOperation(Summary = "Revoke an API key")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> RevokeKey(string institutionId, string keyId)
    {
        var acct = User.GetAccount();
        return (await integrationService.RevokeKeyAsync(institutionId, keyId, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim())).ToActionResult();
    }
}
