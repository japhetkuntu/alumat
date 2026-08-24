using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

namespace ReservEase.Alumni.Platform.Api.Controllers;

[Authorize]
[Route("api/v{version:apiVersion}/plans")]
public class PlansController(IPlanService planService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List subscription plans")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<PlanResponse>>))]
    public async Task<IActionResult> GetPlans()
    {
        var result = await planService.GetPlansAsync();
        return result.ToActionResult();
    }

    [Authorize(Roles = "SuperAdmin,Billing")]
    [HttpPost]
    [SwaggerOperation(Summary = "Create a subscription plan")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<PlanResponse>))]
    public async Task<IActionResult> CreatePlan([FromBody] CreatePlanRequest request)
    {
        var acct = User.GetAccount();
        var result = await planService.CreateAsync(request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }
}
