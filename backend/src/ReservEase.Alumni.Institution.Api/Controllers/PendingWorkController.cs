using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace ReservEase.Alumni.Institution.Api.Controllers;

[Authorize(Roles = "SuperAdmin")]
public class PendingWorkController(IPendingWorkService pending) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Member requests waiting for review", Description = "Counts of what members have submitted and are waiting on an administrator, each with the page that handles it")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IReadOnlyList<PendingWorkItemDto>>))]
    public async Task<IActionResult> Get()
    {
        var institution = HttpContext.Items["Institution"] as PostgresDb.Sdk.Entities.Institution;
        return (await pending.GetAsync(institution?.DisabledFeatures ?? [])).ToOkApiResponse().ToActionResult();
    }
}
