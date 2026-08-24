using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IClassNoteService
{
    Task<IApiResponse<PgPagedResult<ClassNoteDto>>> GetClassNotesAsync(int page, int pageSize, AuthData member, string? communityId = null);
    Task<IApiResponse<ClassNoteDto>> CreateClassNoteAsync(CreateClassNoteRequest request, AuthData member);
    Task<IApiResponse<object>> ToggleLikeAsync(string classNoteId, AuthData member);
    Task<IApiResponse<object>> DeleteClassNoteAsync(string classNoteId, AuthData member);
}

/// <summary>Provide CommunityId to post to that community's wall instead of your institution-wide year-group wall.</summary>
public record CreateClassNoteRequest(string Content, string? ImageUrl, string? CommunityId = null);
