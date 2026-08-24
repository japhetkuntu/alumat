using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IForumService
{
    Task<IApiResponse<PgPagedResult<ForumCategoryDto>>> GetCategoriesAsync(BaseFilter filter);
    Task<IApiResponse<ForumCategoryDto>> CreateCategoryAsync(string name, string? description, AuthData admin);
    Task<IApiResponse<PgPagedResult<ForumThreadDto>>> GetThreadsAsync(ForumThreadFilter filter);
    Task<IApiResponse<object>> PinThreadAsync(string threadId, AuthData admin);
    Task<IApiResponse<object>> CloseThreadAsync(string threadId, AuthData admin);
    Task<IApiResponse<object>> DeleteThreadAsync(string threadId, AuthData admin);
}
