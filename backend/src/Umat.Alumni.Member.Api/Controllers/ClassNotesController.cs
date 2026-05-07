using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Services.Interfaces;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Member.Api.Controllers;

[Authorize]
public class ClassNotesController(IClassNoteService classNoteService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List class notes", Description = "Get a paginated list of class notes for the member's year group")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<ClassNoteDto>>))]
    public async Task<IActionResult> GetClassNotes([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var member = User.GetAccount();
        var result = await classNoteService.GetClassNotesAsync(page, pageSize, member);
        return result.ToActionResult();
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Post class note", Description = "Post a new class note to the member's year group wall")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<ClassNoteDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> CreateClassNote([FromBody] CreateClassNoteRequest request)
    {
        var member = User.GetAccount();
        var result = await classNoteService.CreateClassNoteAsync(request, member);
        return result.ToActionResult();
    }

    [HttpPost("{classNoteId}/like")]
    [SwaggerOperation(Summary = "Toggle like", Description = "Like or unlike a class note")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> ToggleLike(string classNoteId)
    {
        var member = User.GetAccount();
        var result = await classNoteService.ToggleLikeAsync(classNoteId, member);
        return result.ToActionResult();
    }

    [HttpDelete("{classNoteId}")]
    [SwaggerOperation(Summary = "Delete class note", Description = "Soft-delete a class note (author only)")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> DeleteClassNote(string classNoteId)
    {
        var member = User.GetAccount();
        var result = await classNoteService.DeleteClassNoteAsync(classNoteId, member);
        return result.ToActionResult();
    }
}
