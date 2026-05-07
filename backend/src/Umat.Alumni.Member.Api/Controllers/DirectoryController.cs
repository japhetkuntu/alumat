using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Member.Api.Controllers;

[Authorize]
public class DirectoryController(IDirectoryService directoryService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Search directory", Description = "Search the alumni directory with optional filters for department and graduation year")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<DirectoryMemberDto>>))]
    public async Task<IActionResult> SearchMembers([FromQuery] DirectoryFilter filter)
    {
        var result = await directoryService.SearchMembersAsync(filter);
        return result.ToActionResult();
    }
}
