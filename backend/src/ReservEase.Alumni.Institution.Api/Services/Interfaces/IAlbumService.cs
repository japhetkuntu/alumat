using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

public interface IAlbumService
{
    Task<IApiResponse<PgPagedResult<PhotoAlbumDto>>> GetAlbumsAsync(PhotoAlbumFilter filter);
    Task<IApiResponse<PhotoAlbumDto>> GetAlbumByIdAsync(string albumId);
    Task<IApiResponse<PgPagedResult<AlbumPhotoDto>>> GetAlbumPhotosAsync(string albumId, AlbumPhotoFilter filter);
    Task<IApiResponse<PhotoAlbumDto>> CreateAlbumAsync(CreateAlbumRequest request, AuthData admin);
    Task<IApiResponse<PhotoAlbumDto>> UpdateAlbumAsync(string albumId, UpdateAlbumRequest request, AuthData admin);
    Task<IApiResponse<object>> DeleteAlbumAsync(string albumId);
    Task<IApiResponse<AddAlbumPhotosResponse>> AddPhotosAsync(string albumId, AddAlbumPhotosRequest request, AuthData admin);
    Task<IApiResponse<object>> DeletePhotoAsync(string albumId, string photoId);
}
