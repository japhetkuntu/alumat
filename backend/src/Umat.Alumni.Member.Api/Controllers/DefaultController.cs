using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Umat.Alumni.Common.Sdk.Models;

namespace Umat.Alumni.Member.Api.Controllers;

/// <summary>
/// Base controller for all Member API endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces(MediaTypeNames.Application.Json)]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ApiResponse<object>))]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ApiResponse<object>))]
public abstract class DefaultController : ControllerBase
{
}
