using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Extensions;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

/// <summary>Read-only album browsing for members — creating albums and adding/removing photos is Institution.Api-only.</summary>
public class AlbumService(
    IAlumniPgRepository<PhotoAlbum> albumRepo,
    IAlumniPgRepository<AlbumPhoto> photoRepo,
    ILogger<AlbumService> logger) : IAlbumService
{
    public async Task<IApiResponse<PgPagedResult<PhotoAlbumDto>>> GetAlbumsAsync(PhotoAlbumFilter filter)
    {
        try
        {
            logger.LogInformation("GetAlbums request — filter: {Filter}", filter.Serialize());
            var result = await albumRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                a => string.IsNullOrEmpty(filter.Search) || a.Title.Contains(filter.Search));

            var dtoResult = new PgPagedResult<PhotoAlbumDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(a => a.ToDto()).ToList(),
            };
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving photo albums — filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<PhotoAlbumDto>>("Failed to retrieve albums");
        }
    }

    public async Task<IApiResponse<PhotoAlbumDto>> GetAlbumByIdAsync(string albumId)
    {
        try
        {
            var album = await albumRepo.GetByIdAsync(albumId);
            if (album is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<PhotoAlbumDto>("Album not found");

            return album.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving photo album {AlbumId}", albumId);
            return ApiResponseExtensions.ToServerErrorApiResponse<PhotoAlbumDto>("Failed to retrieve album");
        }
    }

    public async Task<IApiResponse<PgPagedResult<AlbumPhotoDto>>> GetAlbumPhotosAsync(string albumId, AlbumPhotoFilter filter)
    {
        try
        {
            var album = await albumRepo.GetByIdAsync(albumId);
            if (album is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<PgPagedResult<AlbumPhotoDto>>("Album not found");

            var result = await photoRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                p => p.AlbumId == albumId);

            var dtoResult = new PgPagedResult<AlbumPhotoDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(p => p.ToDto()).ToList(),
            };
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving photos for album {AlbumId}", albumId);
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<AlbumPhotoDto>>("Failed to retrieve photos");
        }
    }
}
