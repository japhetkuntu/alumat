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
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    ILogger<AlbumService> logger) : IAlbumService
{
    /// <summary>Institution-wide albums (no scope set) are visible to everyone;
    /// a scoped album is visible only to an approved member of its community
    /// or a member whose graduation year is in its batch.</summary>
    private async Task<bool> IsInScopeAsync(PhotoAlbum album, AuthData member)
    {
        if (album.CommunityId is null && album.YearGroups is null)
            return true;

        if (album.YearGroups != null && member.GraduationYear.HasValue && album.YearGroups.Contains(member.GraduationYear.Value))
            return true;

        if (album.CommunityId != null)
        {
            var membership = await membershipRepo.GetOneAsync(m => m.CommunityId == album.CommunityId && m.MemberId == member.Id);
            if (membership is not null && membership.Status == "Approved")
                return true;
        }

        return false;
    }

    public async Task<IApiResponse<PgPagedResult<PhotoAlbumDto>>> GetAlbumsAsync(PhotoAlbumFilter filter, AuthData member)
    {
        try
        {
            logger.LogInformation("GetAlbums request — filter: {Filter} (member: {MemberId})", filter.Serialize(), member.Id);
            var approvedCommunityIds = (await membershipRepo.GetAllAsync(m => m.MemberId == member.Id && m.Status == "Approved"))
                .Select(m => m.CommunityId).ToList();
            var graduationYear = member.GraduationYear;

            var search = filter.Search?.ToLower();
            var result = await albumRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                a => (string.IsNullOrEmpty(search) || a.Title.ToLower().Contains(search))
                  && ((a.CommunityId == null && a.YearGroups == null)
                      || (a.CommunityId != null && approvedCommunityIds.Contains(a.CommunityId))
                      || (a.YearGroups != null && graduationYear.HasValue && a.YearGroups.Contains(graduationYear.Value))));

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

    public async Task<IApiResponse<PhotoAlbumDto>> GetAlbumByIdAsync(string albumId, AuthData member)
    {
        try
        {
            var album = await albumRepo.GetByIdAsync(albumId);
            if (album is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<PhotoAlbumDto>("Album not found");

            if (!await IsInScopeAsync(album, member))
                return ApiResponseExtensions.ToNotFoundApiResponse<PhotoAlbumDto>("Album not found");

            return album.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving photo album {AlbumId}", albumId);
            return ApiResponseExtensions.ToServerErrorApiResponse<PhotoAlbumDto>("Failed to retrieve album");
        }
    }

    public async Task<IApiResponse<PgPagedResult<AlbumPhotoDto>>> GetAlbumPhotosAsync(string albumId, AlbumPhotoFilter filter, AuthData member)
    {
        try
        {
            var album = await albumRepo.GetByIdAsync(albumId);
            if (album is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<PgPagedResult<AlbumPhotoDto>>("Album not found");

            if (!await IsInScopeAsync(album, member))
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
