using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>The platform's record of every payout account that has been set up and approved, whoever it belongs to.</summary>
[Authorize(Roles = "SuperAdmin,Billing")]
[Route("api/v{version:apiVersion}/payout-accounts")]
public class PayoutAccountsController(IPayoutAccountService payoutAccountService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Every payout account on record, filtered by institution, kind and state")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PayoutAccountsResponse>))]
    public async Task<IActionResult> Get([FromQuery] string? institutionId = null, [FromQuery] string? kind = null, [FromQuery] string? state = null,
        [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        => (await payoutAccountService.GetAsync(institutionId, kind, state, search, page, pageSize)).ToActionResult();

    [HttpGet("{kind}/{id}/number")]
    [SwaggerOperation(Summary = "Reveal one account's full number (logged)")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<RevealedAccountNumber>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Reveal(string kind, string id)
    {
        var acct = User.GetAccount();
        return (await payoutAccountService.RevealAsync(kind, id, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim())).ToActionResult();
    }
}
