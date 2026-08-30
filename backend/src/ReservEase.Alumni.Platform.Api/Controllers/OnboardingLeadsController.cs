using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

namespace ReservEase.Alumni.Platform.Api.Controllers;

[Authorize(Roles = "SuperAdmin,Sales")]
public class OnboardingLeadsController(IOnboardingLeadService onboardingLeadService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List onboarding leads")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<OnboardingLeadResponse>>))]
    public async Task<IActionResult> GetLeads([FromQuery] string? status = null)
    {
        var result = await onboardingLeadService.GetLeadsAsync(status);
        return result.ToActionResult();
    }

    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "Get an onboarding lead by id")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<OnboardingLeadResponse>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetLead(string id)
    {
        var result = await onboardingLeadService.GetLeadByIdAsync(id);
        return result.ToActionResult();
    }

    [HttpPatch("{id}/status")]
    [SwaggerOperation(Summary = "Update an onboarding lead's status")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<OnboardingLeadResponse>))]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateOnboardingLeadStatusRequest request)
    {
        var acct = User.GetAccount();
        var result = await onboardingLeadService.UpdateStatusAsync(id, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }

    [HttpPost("{id}/notes")]
    [SwaggerOperation(Summary = "Add an internal note to an onboarding lead")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<OnboardingLeadResponse>))]
    public async Task<IActionResult> AddNote(string id, [FromBody] AddInternalNoteRequest request)
    {
        var acct = User.GetAccount();
        var result = await onboardingLeadService.AddNoteAsync(id, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }
}
