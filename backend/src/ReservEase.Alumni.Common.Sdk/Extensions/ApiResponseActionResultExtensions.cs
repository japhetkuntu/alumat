using Microsoft.AspNetCore.Mvc;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Common.Sdk.Extensions;

public static class ApiResponseActionResultExtensions
{
    public static IActionResult ToActionResult<T>(this IApiResponse<T> response)
    {
        return response.Code switch
        {
            200 => new OkObjectResult(response),
            201 => new ObjectResult(response) { StatusCode = 201 },
            400 => new BadRequestObjectResult(response),
            401 => new UnauthorizedObjectResult(response),
            403 => new ObjectResult(response) { StatusCode = 403 },
            404 => new NotFoundObjectResult(response),
            409 => new ConflictObjectResult(response),
            _ => new ObjectResult(response) { StatusCode = response.Code },
        };
    }
}
