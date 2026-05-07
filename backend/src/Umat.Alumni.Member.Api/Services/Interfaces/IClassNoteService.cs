using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Member.Api.Services.Interfaces;

public interface IClassNoteService
{
    Task<IApiResponse<PgPagedResult<ClassNoteDto>>> GetClassNotesAsync(int page, int pageSize, AuthData member);
    Task<IApiResponse<ClassNoteDto>> CreateClassNoteAsync(CreateClassNoteRequest request, AuthData member);
    Task<IApiResponse<object>> ToggleLikeAsync(string classNoteId, AuthData member);
    Task<IApiResponse<object>> DeleteClassNoteAsync(string classNoteId, AuthData member);
}

public record CreateClassNoteRequest(string Content, string? ImageUrl);
