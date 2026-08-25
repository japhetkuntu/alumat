using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;

namespace ReservEase.Alumni.Member.Api.Controllers;

// No controller-level [RequireFeature] here — Directory (the searchable
// roster) and AlumniMap (the opt-in location map) are independently
// gateable, same pattern as Contributions/ManualPayments.
[Authorize]
public class DirectoryController(IDirectoryService directoryService) : DefaultController
{
    [HttpGet]
    [RequireFeature(InstitutionFeatures.Directory)]
    [SwaggerOperation(Summary = "Search directory", Description = "Search the alumni directory with optional filters for department and graduation year")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<DirectoryMemberDto>>))]
    public async Task<IActionResult> SearchMembers([FromQuery] DirectoryFilter filter)
    {
        var result = await directoryService.SearchMembersAsync(filter);
        return result.ToActionResult();
    }

    [HttpGet("map")]
    [RequireFeature(InstitutionFeatures.AlumniMap)]
    [SwaggerOperation(Summary = "Alumni map", Description = "Get all members who have opted in to appear on the alumni map, with their location")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<AlumniMapMemberDto>>))]
    public async Task<IActionResult> GetMapMembers()
    {
        var result = await directoryService.GetMapMembersAsync();
        return result.ToActionResult();
    }
}
