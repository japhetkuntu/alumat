using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>Which institutions the AlumUnion website features, and which of their numbers it shows.</summary>
[Authorize(Roles = "SuperAdmin,Sales")]
[Route("api/v{version:apiVersion}/showcase")]
public class ShowcaseController(IShowcaseService showcaseService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Every institution with its website-showcase settings and live numbers")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<ShowcaseEntry>>))]
    public async Task<IActionResult> Get() => (await showcaseService.GetAsync()).ToActionResult();

    [HttpPut("{institutionId}")]
    [SwaggerOperation(Summary = "Feature an institution on the website, or take it off")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ShowcaseEntry>))]
    public async Task<IActionResult> Save(string institutionId, [FromBody] SaveShowcaseRequest request)
    {
        var acct = User.GetAccount();
        return (await showcaseService.SaveAsync(institutionId, request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim())).ToActionResult();
    }
}
