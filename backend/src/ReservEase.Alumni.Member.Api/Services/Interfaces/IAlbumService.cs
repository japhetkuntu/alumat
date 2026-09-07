using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IAlbumService
{
    Task<IApiResponse<PgPagedResult<PhotoAlbumDto>>> GetAlbumsAsync(PhotoAlbumFilter filter, AuthData member);
    Task<IApiResponse<PhotoAlbumDto>> GetAlbumByIdAsync(string albumId, AuthData member);
    Task<IApiResponse<PgPagedResult<AlbumPhotoDto>>> GetAlbumPhotosAsync(string albumId, AlbumPhotoFilter filter, AuthData member);
}
