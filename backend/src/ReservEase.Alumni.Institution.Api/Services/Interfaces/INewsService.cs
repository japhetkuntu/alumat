using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface INewsService
{
    Task<IApiResponse<PgPagedResult<NewsPostDto>>> GetPostsAsync(NewsFilter filter, AuthData admin);
    Task<IApiResponse<NewsPostDto>> GetPostAsync(string postId, AuthData admin);
    Task<IApiResponse<NewsPostDto>> CreatePostAsync(CreateNewsPostRequest request, AuthData admin);
    Task<IApiResponse<NewsPostDto>> UpdatePostAsync(UpdateNewsPostRequest request, AuthData admin);
    Task<IApiResponse<NewsPostDto>> PublishPostAsync(string postId, AuthData admin);
    Task<IApiResponse<object>> DeletePostAsync(string postId, AuthData admin);
}
