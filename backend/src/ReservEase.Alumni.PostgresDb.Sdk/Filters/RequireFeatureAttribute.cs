using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Filters;

/// <summary>
/// Blocks a controller/action with 403 when the current request's resolved
/// institution has this feature in <see cref="Institution.DisabledFeatures"/>
/// — see <see cref="InstitutionFeatures"/> for valid keys. This is the
/// server-side half of feature gating; each frontend also hides the
/// corresponding nav item, but this attribute is what makes a disabled
/// feature actually inaccessible rather than just hidden.
///
/// Reads the institution from <c>HttpContext.Items["Institution"]</c>, which
/// TenantResolutionMiddleware populates on every request before MVC action
/// filters run — no extra DB query needed here.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireFeatureAttribute(string feature) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.Items["Institution"] is Institution institution &&
            institution.DisabledFeatures.Contains(feature))
        {
            context.Result = new ObjectResult(new ApiResponse<object>
            {
                Message = $"The {feature} feature is not enabled for this institution.",
                Code = StatusCodes.Status403Forbidden,
            })
            { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next();
    }
}
