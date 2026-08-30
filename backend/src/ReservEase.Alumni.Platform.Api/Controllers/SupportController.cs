using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

namespace ReservEase.Alumni.Platform.Api.Controllers;

[Authorize(Roles = "SuperAdmin,Support")]
[Route("api/v{version:apiVersion}/support-cases")]
public class SupportController(ISupportCaseService supportService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List support cases")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<SupportCaseResponse>>))]
    public async Task<IActionResult> GetCases([FromQuery] string? status = null)
    {
        var result = await supportService.GetCasesAsync(status);
        return result.ToActionResult();
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Create a support case")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<SupportCaseResponse>))]
    public async Task<IActionResult> CreateCase([FromBody] CreateSupportCaseRequest request)
    {
        var result = await supportService.CreateAsync(request, User.GetAccount().Id);
        return result.ToActionResult();
    }

    [HttpPatch("{id}/status")]
    [SwaggerOperation(Summary = "Update a support case's status")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<SupportCaseResponse>))]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateSupportCaseStatusRequest request)
    {
        var acct = User.GetAccount();
        var result = await supportService.UpdateStatusAsync(id, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }

    [HttpPost("{id}/notes")]
    [SwaggerOperation(Summary = "Add an internal note to a support case")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<SupportCaseResponse>))]
    public async Task<IActionResult> AddNote(string id, [FromBody] AddInternalNoteRequest request)
    {
        var acct = User.GetAccount();
        var result = await supportService.AddNoteAsync(id, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }
}
