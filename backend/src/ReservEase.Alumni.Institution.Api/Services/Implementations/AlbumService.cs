using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class AlbumService(
    IAlumniPgRepository<PhotoAlbum> albumRepo,
    IAlumniPgRepository<AlbumPhoto> photoRepo,
    IStorageService storageService,
    ICurrentTenantService currentTenant,
    ILogger<AlbumService> logger) : IAlbumService
{
    public async Task<IApiResponse<PgPagedResult<PhotoAlbumDto>>> GetAlbumsAsync(PhotoAlbumFilter filter, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetAlbums request — filter: {Filter} (admin: {AdminId})", filter.Serialize(), admin.Id);
            var isSuper = admin.Role != StaffRoles.ScopedAdmin;
            var yearGroups = admin.YearGroups ?? new List<int>();
            var communityIds = admin.CommunityIds ?? new List<string>();
            var search = filter.Search?.ToLower();
            var result = await albumRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                a => (string.IsNullOrEmpty(search) || a.Title.ToLower().Contains(search))
                  && (isSuper
                      || (a.YearGroups != null && a.YearGroups.Any(__y => yearGroups.Contains(__y)))
                      || (a.CommunityId != null && communityIds.Contains(a.CommunityId))));

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

    public async Task<IApiResponse<PhotoAlbumDto>> GetAlbumByIdAsync(string albumId, AuthData admin)
    {
        try
        {
            var album = await albumRepo.GetByIdAsync(albumId);
            if (album is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<PhotoAlbumDto>("Album not found");

            if (!admin.CanViewScopedItem(album.YearGroups, album.CommunityId))
                return ApiResponseExtensions.ToNotFoundApiResponse<PhotoAlbumDto>("Album not found");

            return album.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving photo album {AlbumId}", albumId);
            return ApiResponseExtensions.ToServerErrorApiResponse<PhotoAlbumDto>("Failed to retrieve album");
        }
    }

    public async Task<IApiResponse<PgPagedResult<AlbumPhotoDto>>> GetAlbumPhotosAsync(string albumId, AlbumPhotoFilter filter, AuthData admin)
    {
        try
        {
            var album = await albumRepo.GetByIdAsync(albumId);
            if (album is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<PgPagedResult<AlbumPhotoDto>>("Album not found");

            if (!admin.CanViewScopedItem(album.YearGroups, album.CommunityId))
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

    public async Task<IApiResponse<PhotoAlbumDto>> CreateAlbumAsync(CreateAlbumRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("CreateAlbum request: {Request} by admin {AdminId}", request.Serialize(), admin.Id);

            if (string.IsNullOrWhiteSpace(request.Title))
                return ApiResponseExtensions.ToBadRequestApiResponse<PhotoAlbumDto>("Title is required.");

            var yearGroups = admin.ResolveYearGroupsForCreation(request.YearGroups);
            var communityId = admin.ResolveCommunityForCreation(request.CommunityId);
            ScopeAuthorizationExtensions.NormalizeAudience(ref yearGroups, ref communityId);

            var album = new PhotoAlbum
            {
                Title = request.Title,
                Description = request.Description,
                PhotoCount = 0,
                CommunityId = communityId,
                YearGroups = yearGroups,
                CreatedBy = admin.Id,
            };

            await albumRepo.AddAsync(album);

            logger.LogInformation("Photo album {AlbumId} created by admin {AdminId}", album.Id, admin.Id);
            return album.ToDto().ToCreatedApiResponse("Album created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating photo album: {Request} by admin {AdminId}", request.Serialize(), admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<PhotoAlbumDto>("Failed to create album");
        }
    }

    public async Task<IApiResponse<PhotoAlbumDto>> UpdateAlbumAsync(string albumId, UpdateAlbumRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("UpdateAlbum request for albumId: {AlbumId} by admin {AdminId}", albumId, admin.Id);
            var album = await albumRepo.GetByIdAsync(albumId);
            if (album is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<PhotoAlbumDto>("Album not found");

            if (!admin.CanModifyScopedItem(album.YearGroups, album.CreatedBy, album.CommunityId))
                return ApiResponseExtensions.ToNotFoundApiResponse<PhotoAlbumDto>("Album not found");

            if (string.IsNullOrWhiteSpace(request.Title))
                return ApiResponseExtensions.ToBadRequestApiResponse<PhotoAlbumDto>("Title is required.");

            if (!string.IsNullOrEmpty(request.CoverImageUrl))
            {
                var matches = await photoRepo.GetOneAsync(p => p.AlbumId == albumId && p.Url == request.CoverImageUrl);
                if (matches is null)
                    return ApiResponseExtensions.ToBadRequestApiResponse<PhotoAlbumDto>("CoverImageUrl must match one of this album's existing photos.");
            }

            var updatedYearGroups = admin.ResolveYearGroupsForCreation(request.YearGroups);
            var updatedCommunityId = admin.ResolveCommunityForCreation(request.CommunityId);
            ScopeAuthorizationExtensions.NormalizeAudience(ref updatedYearGroups, ref updatedCommunityId);

            album.Title = request.Title;
            album.Description = request.Description;
            album.CoverImageUrl = request.CoverImageUrl;
            album.CommunityId = updatedCommunityId;
            album.YearGroups = updatedYearGroups;
            album.UpdatedAt = DateTime.UtcNow;
            album.UpdatedBy = admin.Id;
            await albumRepo.UpdateAsync(album);

            logger.LogInformation("Photo album {AlbumId} updated by admin {AdminId}", album.Id, admin.Id);
            return album.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating photo album {AlbumId} by admin {AdminId}", albumId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<PhotoAlbumDto>("Failed to update album");
        }
    }

    public async Task<IApiResponse<object>> DeleteAlbumAsync(string albumId, AuthData admin)
    {
        try
        {
            var album = await albumRepo.GetByIdAsync(albumId);
            if (album is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Album not found");

            if (!admin.CanModifyScopedItem(album.YearGroups, album.CreatedBy, album.CommunityId))
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Album not found");

            var photos = (await photoRepo.GetAllAsync(p => p.AlbumId == albumId)).ToList();
            foreach (var photo in photos)
                await photoRepo.RemoveAsync(photo);

            await albumRepo.RemoveAsync(album);
            logger.LogInformation("Photo album {AlbumId} deleted along with {PhotoCount} photos", albumId, photos.Count);
            return new object().ToOkApiResponse("Album deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting photo album {AlbumId}", albumId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete album");
        }
    }

    public async Task<IApiResponse<AddAlbumPhotosResponse>> AddPhotosAsync(string albumId, AddAlbumPhotosRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("AddPhotos request for albumId: {AlbumId} by admin {AdminId}", albumId, admin.Id);
            var album = await albumRepo.GetByIdAsync(albumId);
            if (album is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<AddAlbumPhotosResponse>("Album not found");

            if (!admin.CanModifyScopedItem(album.YearGroups, album.CreatedBy, album.CommunityId))
                return ApiResponseExtensions.ToNotFoundApiResponse<AddAlbumPhotosResponse>("Album not found");

            if (request.Photos is not { Count: > 0 })
                return ApiResponseExtensions.ToBadRequestApiResponse<AddAlbumPhotosResponse>("At least one photo is required.");

            var uploadedUrls = await storageService.BulkUploadFilesAsync(request.Photos, folderName: "albums", institutionSlug: currentTenant.InstitutionSlug ?? "");

            var newPhotos = uploadedUrls.Select(url => new AlbumPhoto
            {
                AlbumId = albumId,
                Url = url,
                CreatedBy = admin.Id,
            }).ToList();

            await photoRepo.AddRangeAsync(newPhotos);

            album.PhotoCount += newPhotos.Count;
            if (string.IsNullOrEmpty(album.CoverImageUrl) && newPhotos.Count > 0)
                album.CoverImageUrl = newPhotos[0].Url;
            album.UpdatedAt = DateTime.UtcNow;
            album.UpdatedBy = admin.Id;
            await albumRepo.UpdateAsync(album);

            logger.LogInformation("{Count} photos added to album {AlbumId} by admin {AdminId}", newPhotos.Count, albumId, admin.Id);
            return new AddAlbumPhotosResponse
            {
                Album = album.ToDto(),
                AddedPhotos = newPhotos.Select(p => p.ToDto()).ToList(),
            }.ToCreatedApiResponse("Photos added");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error adding photos to album {AlbumId} by admin {AdminId}", albumId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<AddAlbumPhotosResponse>("Failed to add photos");
        }
    }

    public async Task<IApiResponse<object>> DeletePhotoAsync(string albumId, string photoId, AuthData admin)
    {
        try
        {
            var album = await albumRepo.GetByIdAsync(albumId);
            if (album is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Album not found");

            if (!admin.CanModifyScopedItem(album.YearGroups, album.CreatedBy, album.CommunityId))
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Album not found");

            var photo = await photoRepo.GetByIdAsync(photoId);
            if (photo is null || photo.AlbumId != albumId)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Photo not found");

            await photoRepo.RemoveAsync(photo);

            album.PhotoCount = Math.Max(0, album.PhotoCount - 1);
            if (album.CoverImageUrl == photo.Url)
            {
                var nextOldest = (await photoRepo.GetAllAsync(p => p.AlbumId == albumId))
                    .OrderBy(p => p.CreatedAt)
                    .FirstOrDefault();
                album.CoverImageUrl = nextOldest?.Url;
            }
            album.UpdatedAt = DateTime.UtcNow;
            await albumRepo.UpdateAsync(album);

            logger.LogInformation("Photo {PhotoId} deleted from album {AlbumId}", photoId, albumId);
            return new object().ToOkApiResponse("Photo deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting photo {PhotoId} from album {AlbumId}", photoId, albumId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete photo");
        }
    }
}
