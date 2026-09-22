using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>Cross-institution alumni visibility for platform staff — who every institution's members are, and which of them are actually active.</summary>
[Authorize(Roles = "SuperAdmin,Support")]
[Route("api/v{version:apiVersion}/members")]
public class MembersController(IPlatformMemberService memberService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List members across every institution")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<PlatformMemberListItem>>))]
    public async Task<IActionResult> GetMembers(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null,
        [FromQuery] string? institutionId = null, [FromQuery] string? status = null, [FromQuery] bool? activeOnly = null)
    {
        var filter = new PlatformMemberFilter
        {
            Page = page, PageSize = pageSize, Search = search,
            InstitutionId = institutionId, Status = status, ActiveOnly = activeOnly,
        };
        var result = await memberService.GetMembersAsync(filter);
        return result.ToActionResult();
    }

    [HttpPatch("{id}/profile")]
    [SwaggerOperation(Summary = "Update a member's community profile and directory visibility")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> UpdateMemberProfile(string id, [FromBody] UpdatePlatformMemberProfileRequest request)
    {
        var acct = User.GetAccount();
        var result = await memberService.UpdateMemberProfileAsync(id, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }
}
