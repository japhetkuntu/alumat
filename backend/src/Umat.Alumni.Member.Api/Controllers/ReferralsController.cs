using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;

namespace Umat.Alumni.Member.Api.Controllers;

[Authorize]
public class ReferralsController(IReferralService referralService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Referral info", Description = "Get the current member's referral code and stats")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> GetReferralInfo()
    {
        var member = User.GetAccount();
        var result = await referralService.GetMyReferralInfoAsync(member);
        return result.ToActionResult();
    }

    [HttpPost("invite")]
    [SwaggerOperation(Summary = "Send referral invitation", Description = "Send a referral invitation email to a potential member")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Invite([FromBody] InviteRequest request)
    {
        var member = User.GetAccount();
        var result = await referralService.InviteAsync(request.Email, member);
        return result.ToActionResult();
    }

    [HttpGet("list")]
    [SwaggerOperation(Summary = "My referrals", Description = "Get all referrals made by the current member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<ReferralDto>>))]
    public async Task<IActionResult> GetMyReferrals()
    {
        var member = User.GetAccount();
        var result = await referralService.GetMyReferralsAsync(member.Id);
        return result.ToActionResult();
    }
}

public record InviteRequest(string Email);
