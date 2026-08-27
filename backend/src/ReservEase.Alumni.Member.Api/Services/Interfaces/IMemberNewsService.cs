using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IMemberNewsService
{
    Task<IApiResponse<PgPagedResult<NewsPostDto>>> GetPostsAsync(NewsFilter filter, string memberId);
    Task<IApiResponse<NewsPostDto>> GetPostByIdAsync(string postId, string memberId);
}
