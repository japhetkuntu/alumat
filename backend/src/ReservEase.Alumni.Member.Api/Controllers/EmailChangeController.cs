using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>Changing the email a member signs in with: a code to the current address, then a code to the new one.</summary>
[Route("api/v{version:apiVersion}/auth/email-change")]
public class EmailChangeController(IEmailChangeService service) : DefaultController
{
    [HttpGet]
    [Authorize]
    [SwaggerOperation(Summary = "Email change status", Description = "Where a change in progress stands, if there is one")]
    public async Task<IActionResult> Status() => (await service.GetStatusAsync(User.GetAccount())).ToActionResult();

    [HttpPost("start")]
    [Authorize]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
    [SwaggerOperation(Summary = "Start an email change", Description = "Sends a code to the member's current address")]
    public async Task<IActionResult> Start([FromBody] StartEmailChangeRequest request)
        => (await service.StartAsync(request, User.GetAccount())).ToActionResult();

    [HttpPost("resend")]
    [Authorize]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
    [SwaggerOperation(Summary = "Resend the code", Description = "Sends a new code for the step in progress (a few times at most)")]
    public async Task<IActionResult> Resend() => (await service.ResendAsync(User.GetAccount())).ToActionResult();

    [HttpPost("confirm-current")]
    [Authorize]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
    [SwaggerOperation(Summary = "Confirm the current address", Description = "Checks the code sent to the current address, then sends one to the new address")]
    public async Task<IActionResult> ConfirmCurrent([FromBody] EmailChangeCodeRequest request)
        => (await service.ConfirmCurrentAsync(request, User.GetAccount())).ToActionResult();

    [HttpPost("confirm-new")]
    [Authorize]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
    [SwaggerOperation(Summary = "Confirm the new address", Description = "Checks the code sent to the new address and changes the email")]
    public async Task<IActionResult> ConfirmNew([FromBody] EmailChangeCodeRequest request)
        => (await service.ConfirmNewAsync(request, User.GetAccount())).ToActionResult();

    [HttpDelete]
    [Authorize]
    [SwaggerOperation(Summary = "Cancel an email change")]
    public async Task<IActionResult> Cancel() => (await service.CancelAsync(User.GetAccount())).ToActionResult();

    [HttpPost("undo")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
    [SwaggerOperation(Summary = "Undo an email change", Description = "Restores the old address from the link sent to it")]
    public async Task<IActionResult> Undo([FromBody] UndoEmailChangeRequest request)
        => (await service.UndoAsync(request)).ToActionResult();
}
