using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class InstitutionSpotlightService(
    IAlumniPgRepository<Spotlight> spotlightRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    ITemporalClientProvider temporalProvider,
    ICurrentTenantService currentTenant,
    IRedisService<PublicContentCacheConfig> publicCache,
    ILogger<InstitutionSpotlightService> logger) : IInstitutionSpotlightService
{
    private Task InvalidatePublicSpotlightsCacheAsync()
        => currentTenant.InstitutionId is { } id ? publicCache.RemoveAsync(PublicContentCacheKeys.Spotlights(id)) : Task.CompletedTask;

    public async Task<IApiResponse<PgPagedResult<SpotlightDto>>> GetSpotlightsAsync(int page, int pageSize, string? status)
    {
        try
        {
            // Archived spotlights are excluded from the default "All" view — an
            // admin has to explicitly filter for Archived to see them, mirroring
            // how a trash/archive folder normally stays out of the way.
            var result = await spotlightRepo.GetPagedAsync(
                page, pageSize, "CreatedAt", "desc",
                status is not null ? s => s.Status == status : s => s.Status != "Archived");

            var dtos = result.Results.Select(s => s.ToDto()).ToList();

            var memberIds = dtos.Select(d => d.MemberId).Distinct().ToList();
            var members = (await memberRepo.GetAllAsync(m => memberIds.Contains(m.Id))).ToDictionary(m => m.Id);
            foreach (var dto in dtos)
            {
                if (members.TryGetValue(dto.MemberId, out var m))
                    dto.MemberGraduationYear = m.GraduationYear;
            }

            return new PgPagedResult<SpotlightDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = dtos,
            }.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving spotlights");
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<SpotlightDto>>("Failed to retrieve spotlights");
        }
    }

    public async Task<IApiResponse<SpotlightDto>> CreateSpotlightAsync(AdminCreateSpotlightRequest request, AuthData admin)
    {
        try
        {
            var member = await memberRepo.GetByIdAsync(request.MemberId);
            if (member is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<SpotlightDto>("Member not found");

            var spotlight = new Spotlight
            {
                MemberId = member.Id,
                Member = new MemberSnapshot
                {
                    Id = member.Id,
                    FirstName = member.FirstName,
                    LastName = member.LastName,
                    Email = member.Email,
                    ProfilePictureUrl = member.ProfilePictureUrl,
                    MemberNumber = member.MemberNumber,
                },
                Title = request.Title,
                Story = request.Story,
                ImageUrl = request.ImageUrl,
                Status = "Approved",
                FeaturedMonth = DateTime.UtcNow,
                CreatedBy = admin.Id,
                UpdatedBy = admin.Id,
            };

            await spotlightRepo.AddAsync(spotlight);
            await InvalidatePublicSpotlightsCacheAsync();

            await temporalProvider.EnqueueNotificationAsync(NotificationRequest.SpotlightAlert(currentTenant.InstitutionId!, spotlight.Id), logger);

            var dto = spotlight.ToDto();
            dto.MemberGraduationYear = member.GraduationYear;
            return dto.ToOkApiResponse("Spotlight created and featured.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating spotlight for member {MemberId}", request.MemberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<SpotlightDto>("Failed to create spotlight");
        }
    }

    public async Task<IApiResponse<SpotlightDto>> UpdateSpotlightAsync(string spotlightId, UpdateSpotlightRequest request, AuthData admin)
    {
        try
        {
            var spotlight = await spotlightRepo.GetByIdAsync(spotlightId);
            if (spotlight is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<SpotlightDto>("Spotlight not found");

            spotlight.Title = request.Title;
            spotlight.Story = request.Story;
            spotlight.ImageUrl = request.ImageUrl;
            spotlight.UpdatedBy = admin.Id;
            await spotlightRepo.UpdateAsync(spotlight);
            await InvalidatePublicSpotlightsCacheAsync();

            return spotlight.ToDto().ToOkApiResponse("Spotlight updated.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating spotlight {SpotlightId}", spotlightId);
            return ApiResponseExtensions.ToServerErrorApiResponse<SpotlightDto>("Failed to update spotlight");
        }
    }

    public async Task<IApiResponse<SpotlightDto>> ApproveSpotlightAsync(string spotlightId, AuthData admin)
    {
        try
        {
            var spotlight = await spotlightRepo.GetByIdAsync(spotlightId);
            if (spotlight is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<SpotlightDto>("Spotlight not found");

            spotlight.Status = "Approved";
            spotlight.FeaturedMonth = DateTime.UtcNow;
            spotlight.UpdatedBy = admin.Id;
            await spotlightRepo.UpdateAsync(spotlight);
            await InvalidatePublicSpotlightsCacheAsync();

            await temporalProvider.EnqueueNotificationAsync(
                NotificationRequest.SpotlightDecision(currentTenant.InstitutionId!, spotlight.MemberId, true, null, spotlight.Id), logger);
            await temporalProvider.EnqueueNotificationAsync(NotificationRequest.SpotlightAlert(currentTenant.InstitutionId!, spotlight.Id), logger);

            return spotlight.ToDto().ToOkApiResponse("Spotlight approved.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error approving spotlight {SpotlightId}", spotlightId);
            return ApiResponseExtensions.ToServerErrorApiResponse<SpotlightDto>("Failed to approve spotlight");
        }
    }

    public async Task<IApiResponse<SpotlightDto>> RejectSpotlightAsync(string spotlightId, string? reason, AuthData admin)
    {
        try
        {
            var spotlight = await spotlightRepo.GetByIdAsync(spotlightId);
            if (spotlight is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<SpotlightDto>("Spotlight not found");

            spotlight.Status = "Rejected";
            spotlight.AdminNotes = reason;
            spotlight.UpdatedBy = admin.Id;
            await spotlightRepo.UpdateAsync(spotlight);
            await InvalidatePublicSpotlightsCacheAsync();

            await temporalProvider.EnqueueNotificationAsync(
                NotificationRequest.SpotlightDecision(currentTenant.InstitutionId!, spotlight.MemberId, false, reason, spotlight.Id), logger);

            return spotlight.ToDto().ToOkApiResponse("Spotlight rejected.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error rejecting spotlight {SpotlightId}", spotlightId);
            return ApiResponseExtensions.ToServerErrorApiResponse<SpotlightDto>("Failed to reject spotlight");
        }
    }

    /// <summary>
    /// Soft-removes a spotlight from both the admin's default list and the
    /// public site without deleting it — for old/unwanted spotlights an
    /// admin doesn't want surfacing anywhere anymore, but may still want to
    /// keep on record.
    /// </summary>
    public async Task<IApiResponse<SpotlightDto>> ArchiveSpotlightAsync(string spotlightId, AuthData admin)
    {
        try
        {
            var spotlight = await spotlightRepo.GetByIdAsync(spotlightId);
            if (spotlight is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<SpotlightDto>("Spotlight not found");

            spotlight.Status = "Archived";
            spotlight.IsFeatured = false;
            spotlight.UpdatedBy = admin.Id;
            await spotlightRepo.UpdateAsync(spotlight);
            await InvalidatePublicSpotlightsCacheAsync();

            return spotlight.ToDto().ToOkApiResponse("Spotlight archived.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error archiving spotlight {SpotlightId}", spotlightId);
            return ApiResponseExtensions.ToServerErrorApiResponse<SpotlightDto>("Failed to archive spotlight");
        }
    }

    /// <summary>
    /// Explicitly picks which one approved spotlight is shown on the public
    /// landing page, rather than leaving it to implicit FeaturedMonth/
    /// CreatedAt ordering (see PublicController.GetPublicSpotlights). Only
    /// one spotlight per institution should carry this at a time, so
    /// featuring one clears it from every other spotlight first.
    /// </summary>
    public async Task<IApiResponse<SpotlightDto>> SetFeaturedAsync(string spotlightId, AuthData admin)
    {
        try
        {
            var spotlight = await spotlightRepo.GetByIdAsync(spotlightId);
            if (spotlight is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<SpotlightDto>("Spotlight not found");
            if (spotlight.Status != "Approved")
                return ApiResponseExtensions.ToBadRequestApiResponse<SpotlightDto>("Only an approved spotlight can be featured");

            var currentlyFeatured = (await spotlightRepo.GetAllAsync(s => s.IsFeatured && s.Id != spotlightId)).ToList();
            foreach (var other in currentlyFeatured)
            {
                other.IsFeatured = false;
                other.UpdatedBy = admin.Id;
            }
            if (currentlyFeatured.Count > 0)
                await spotlightRepo.UpdateRangeAsync(currentlyFeatured);

            spotlight.IsFeatured = true;
            spotlight.UpdatedBy = admin.Id;
            await spotlightRepo.UpdateAsync(spotlight);
            await InvalidatePublicSpotlightsCacheAsync();

            return spotlight.ToDto().ToOkApiResponse("Spotlight featured on your public site.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error featuring spotlight {SpotlightId}", spotlightId);
            return ApiResponseExtensions.ToServerErrorApiResponse<SpotlightDto>("Failed to feature spotlight");
        }
    }

    public async Task<IApiResponse<SpotlightDto>> UnfeatureSpotlightAsync(string spotlightId, AuthData admin)
    {
        try
        {
            var spotlight = await spotlightRepo.GetByIdAsync(spotlightId);
            if (spotlight is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<SpotlightDto>("Spotlight not found");

            spotlight.IsFeatured = false;
            spotlight.UpdatedBy = admin.Id;
            await spotlightRepo.UpdateAsync(spotlight);
            await InvalidatePublicSpotlightsCacheAsync();

            return spotlight.ToDto().ToOkApiResponse("Spotlight unfeatured.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error unfeaturing spotlight {SpotlightId}", spotlightId);
            return ApiResponseExtensions.ToServerErrorApiResponse<SpotlightDto>("Failed to unfeature spotlight");
        }
    }
}
