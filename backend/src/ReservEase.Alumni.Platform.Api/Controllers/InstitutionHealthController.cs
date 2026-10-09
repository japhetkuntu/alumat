using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>How institutions are doing, for the people who help them succeed. Aggregates only: no member information.</summary>
[Authorize(Roles = "SuperAdmin,Support")]
[Route("api/v{version:apiVersion}/institution-health")]
public class InstitutionHealthController(IInstitutionHealthService health) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Institution health overview", Description = "Every live institution with its latest community health reading, administrator activity, waiting suggestions and follow-up tasks, and why it needs attention if it does")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<InstitutionHealthDto>))]
    public async Task<IActionResult> Get() => (await health.GetAsync()).ToActionResult();
}
