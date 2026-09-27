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

    [HttpPost]
    [SwaggerOperation(Summary = "Log an onboarding lead from outreach, a warm intro or a referral")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<OnboardingLeadResponse>))]
    public async Task<IActionResult> CreateLead([FromBody] CreateStaffOnboardingLeadRequest request)
    {
        var acct = User.GetAccount();
        var result = await onboardingLeadService.CreateByStaffAsync(request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }

    [HttpGet("assignees")]
    [SwaggerOperation(Summary = "Platform staff who can own onboarding leads")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<LeadAssigneeResponse>>))]
    public async Task<IActionResult> GetAssignees()
    {
        var result = await onboardingLeadService.GetAssigneesAsync();
        return result.ToActionResult();
    }

    [HttpPost("import")]
    [SwaggerOperation(Summary = "Bulk-import onboarding leads (e.g. an outreach target list)")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ImportOnboardingLeadsResponse>))]
    public async Task<IActionResult> Import([FromBody] ImportOnboardingLeadsRequest request)
    {
        var acct = User.GetAccount();
        var result = await onboardingLeadService.ImportAsync(request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }

    [HttpPut("{id}")]
    [SwaggerOperation(Summary = "Edit an onboarding lead's details, owner and follow-up date")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<OnboardingLeadResponse>))]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateOnboardingLeadRequest request)
    {
        var acct = User.GetAccount();
        var result = await onboardingLeadService.UpdateAsync(id, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
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
