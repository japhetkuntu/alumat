using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using Swashbuckle.AspNetCore.Annotations;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>Example content for a community that has none yet: added on request, removed in one step. Full administrators only.</summary>
[Authorize(Roles = "SuperAdmin")]
[Route("api/v{version:apiVersion}/institution/me/sample-content")]
public class SampleContentController(ISampleContentService sampleContent) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Whether example content is present")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<SampleContentStatus>))]
    public async Task<IActionResult> Status() => (await sampleContent.GetStatusAsync()).ToActionResult();

    [HttpPost]
    [SwaggerOperation(Summary = "Add example content", Description = "A welcome post, a guide post and an upcoming event, clearly marked as examples. Nobody is notified.")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<SampleContentStatus>))]
    public async Task<IActionResult> Add() => (await sampleContent.AddAsync(User.GetAccount().Id)).ToActionResult();

    [HttpDelete]
    [SwaggerOperation(Summary = "Remove all example content", Description = "Removes only what was added as example content, never anything a person wrote.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<SampleContentStatus>))]
    public async Task<IActionResult> Remove() => (await sampleContent.RemoveAsync()).ToActionResult();
}
