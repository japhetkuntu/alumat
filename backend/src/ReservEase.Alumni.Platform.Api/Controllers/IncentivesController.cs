using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using Swashbuckle.AspNetCore.Annotations;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>
/// What representatives have earned and been paid. Settlement is entered by hand, so only a SuperAdmin records anything;
/// a representative can read their own lines.
/// </summary>
[Authorize(Roles = "SuperAdmin,Representative")]
public class IncentivesController(IIncentiveService incentives) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Incentive balances and ledger lines (everyone's for a SuperAdmin, your own for a representative)")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IncentivesViewDto>))]
    public async Task<IActionResult> Get() => (await incentives.GetAsync()).ToActionResult();

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    [SwaggerOperation(Summary = "Record something a representative earned, or a payment made to them")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<IncentiveEntryDto>))]
    public async Task<IActionResult> Add([FromBody] AddIncentiveEntryRequest request)
    {
        var acct = User.GetAccount();
        return (await incentives.AddAsync(request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim())).ToActionResult();
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost("{id}/void")]
    [SwaggerOperation(Summary = "Void an entry recorded by mistake (it stays in the history)")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IncentiveEntryDto>))]
    public async Task<IActionResult> Void(string id, [FromBody] VoidIncentiveEntryRequest request)
    {
        var acct = User.GetAccount();
        return (await incentives.VoidAsync(id, request.Reason, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim())).ToActionResult();
    }
}
