using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Models;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Member.Api.Services.Interfaces;

public interface IMemberForumService
{
    Task<IApiResponse<PgPagedResult<ForumCategoryDto>>> GetCategoriesAsync();
    Task<IApiResponse<PgPagedResult<ForumThreadDto>>> GetThreadsAsync(ForumThreadFilter filter);
    Task<IApiResponse<ForumThreadDto>> CreateThreadAsync(CreateThreadRequest request, AuthData member);
    Task<IApiResponse<PgPagedResult<ForumPostDto>>> GetPostsAsync(string threadId, BaseFilter filter);
    Task<IApiResponse<ForumPostDto>> ReplyToThreadAsync(CreateForumPostRequest request, AuthData member);
}
