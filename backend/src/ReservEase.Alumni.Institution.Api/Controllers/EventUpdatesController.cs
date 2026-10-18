using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>Updates the organiser posts to an event for the people who are going.</summary>
[Authorize(Roles = "SuperAdmin,ScopedAdmin")]
[RequireFeature(InstitutionFeatures.Events)]
[Route("api/v{version:apiVersion}/events/{eventId}/updates")]
public class EventUpdatesController(IEventUpdateService updateService) : DefaultController
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [HttpGet]
    [SwaggerOperation(Summary = "List an event's updates")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<EventUpdateDto>>))]
    public async Task<IActionResult> List(string eventId)
        => (await updateService.ListAsync(eventId, User.GetAccount())).ToActionResult();

    /// <summary>
    /// Post an update. Multipart: Title, Category, BlocksJson (the parts, as JSON), and any new files, each sent as a form part
    /// named with the id of the part it belongs to.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(EventUpdateRules.MaxUploadBytes + 5 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = EventUpdateRules.MaxUploadBytes + 5 * 1024 * 1024, ValueCountLimit = 4000)]
    [SwaggerOperation(Summary = "Post an event update")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<EventUpdateDto>))]
    public async Task<IActionResult> Create(string eventId, [FromForm] string title, [FromForm] string category, [FromForm] string blocksJson)
    {
        var request = ReadRequest(title, category, blocksJson);
        if (request is null) return BadRequest(new { message = "Could not read the update. Please try again." });
        return (await updateService.CreateAsync(eventId, request, FilesByBlock(), User.GetAccount())).ToActionResult();
    }

    [HttpPut("{updateId}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(EventUpdateRules.MaxUploadBytes + 5 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = EventUpdateRules.MaxUploadBytes + 5 * 1024 * 1024, ValueCountLimit = 4000)]
    [SwaggerOperation(Summary = "Edit an event update")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<EventUpdateDto>))]
    public async Task<IActionResult> Update(string eventId, string updateId, [FromForm] string title, [FromForm] string category, [FromForm] string blocksJson)
    {
        var request = ReadRequest(title, category, blocksJson);
        if (request is null) return BadRequest(new { message = "Could not read the update. Please try again." });
        return (await updateService.UpdateAsync(eventId, updateId, request, FilesByBlock(), User.GetAccount())).ToActionResult();
    }

    [HttpPut("{updateId}/pin")]
    [SwaggerOperation(Summary = "Pin or unpin an event update")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<EventUpdateDto>))]
    public async Task<IActionResult> Pin(string eventId, string updateId, [FromQuery] bool pinned = true)
        => (await updateService.SetPinnedAsync(eventId, updateId, pinned, User.GetAccount())).ToActionResult();

    [HttpDelete("{updateId}")]
    [SwaggerOperation(Summary = "Delete an event update")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Delete(string eventId, string updateId)
        => (await updateService.DeleteAsync(eventId, updateId, User.GetAccount())).ToActionResult();

    private static EventUpdateSaveRequest? ReadRequest(string title, string category, string blocksJson)
    {
        try
        {
            var blocks = JsonSerializer.Deserialize<List<EventUpdateBlock>>(blocksJson, Json);
            return blocks is null ? null : new EventUpdateSaveRequest { Title = title, Category = category, Blocks = blocks };
        }
        catch (JsonException) { return null; }
    }

    /// <summary>The uploaded files, grouped by the form-part name, which is the id of the update part each belongs to.</summary>
    private Dictionary<string, List<IFormFile>> FilesByBlock()
        => Request.Form.Files.GroupBy(f => f.Name).ToDictionary(g => g.Key, g => g.ToList());
}
