using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using Swashbuckle.AspNetCore.Annotations;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>The institution's structure in one read: administrators, groups, communities and who looks after what. Full administrators only.</summary>
[Authorize(Roles = "SuperAdmin")]
[Route("api/v{version:apiVersion}/structure")]
public class StructureController(IStructureService structure) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Institution structure", Description = "Administrators, groups and communities with counts, and which of them have no administrator")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<StructureDto>))]
    public async Task<IActionResult> Get() => (await structure.GetAsync()).ToActionResult();
}
