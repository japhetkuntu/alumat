using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Extensions;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class BusinessDirectoryService(
    IAlumniPgRepository<BusinessListing> listingRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IStorageService storageService,
    ICurrentTenantService currentTenant,
    ILogger<BusinessDirectoryService> logger) : IBusinessDirectoryService
{
    public async Task<IApiResponse<PgPagedResult<BusinessListingDto>>> GetListingsAsync(BusinessListingFilter filter)
    {
        try
        {
            logger.LogInformation("GetListings request — filter: {Filter}", filter.Serialize());
            var result = await listingRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                l => l.Status == "Approved" && !l.IsHiddenByMember
                     && (string.IsNullOrEmpty(filter.Search) || l.BusinessName.Contains(filter.Search)));

            var dtoResult = new PgPagedResult<BusinessListingDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(l => l.ToDto()).ToList(),
            };
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving business listings — filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<BusinessListingDto>>("Failed to retrieve business listings");
        }
    }

    public async Task<IApiResponse<BusinessListingDto>> GetListingByIdAsync(string listingId)
    {
        try
        {
            var listing = await listingRepo.GetByIdAsync(listingId);
            if (listing is null || listing.Status != "Approved" || listing.IsHiddenByMember)
                return ApiResponseExtensions.ToNotFoundApiResponse<BusinessListingDto>("Business listing not found");

            return listing.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving business listing {ListingId}", listingId);
            return ApiResponseExtensions.ToServerErrorApiResponse<BusinessListingDto>("Failed to retrieve business listing");
        }
    }

    public async Task<IApiResponse<List<BusinessListingDto>>> GetMyListingsAsync(string memberId)
    {
        try
        {
            var listings = await listingRepo.GetAllAsync(l => l.MemberId == memberId);
            return listings.Select(l => l.ToDto()).OrderByDescending(l => l.CreatedAt).ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving business listings for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<BusinessListingDto>>("Failed to retrieve your business listings");
        }
    }

    public async Task<IApiResponse<BusinessListingDto>> SubmitListingAsync(SubmitBusinessListingRequest request, AuthData member)
    {
        try
        {
            logger.LogInformation("SubmitListing request: {Request} by member {MemberId}", request.Serialize(), member.Id);

            // A member has at most one business listing — block a second submission while any existing one exists.
            var existing = await listingRepo.GetOneAsync(l => l.MemberId == member.Id);
            if (existing is not null)
                return ApiResponseExtensions.ToConflictApiResponse<BusinessListingDto>("You already have a business listing — edit your existing listing instead of submitting a new one.");

            var contactError = ValidateContact(request.PhoneNumber, request.Email, request.WebsiteUrl);
            if (contactError is not null)
                return ApiResponseExtensions.ToBadRequestApiResponse<BusinessListingDto>(contactError);

            if (string.IsNullOrWhiteSpace(request.BusinessName))
                return ApiResponseExtensions.ToBadRequestApiResponse<BusinessListingDto>("Business name is required.");

            var memberEntity = await memberRepo.GetByIdAsync(member.Id);
            if (memberEntity is null)
                return ApiResponseExtensions.ToBadRequestApiResponse<BusinessListingDto>("Member not found.");

            var listing = new BusinessListing
            {
                MemberId = member.Id,
                Member = new MemberSnapshot
                {
                    Id = memberEntity.Id,
                    FirstName = memberEntity.FirstName,
                    LastName = memberEntity.LastName,
                    Email = memberEntity.Email,
                    ProfilePictureUrl = memberEntity.ProfilePictureUrl,
                    MemberNumber = memberEntity.MemberNumber,
                },
                BusinessName = request.BusinessName,
                Description = request.Description,
                Location = request.Location,
                PhoneNumber = request.PhoneNumber,
                Email = request.Email,
                WebsiteUrl = request.WebsiteUrl,
                ExternalLinkUrl = request.ExternalLinkUrl,
                Status = "Pending",
                CreatedBy = member.Id,
            };

            if (request.Logo is not null)
            {
                var name = $"{Guid.NewGuid():N}{Path.GetExtension(request.Logo.FileName)}";
                listing.LogoUrl = await storageService.UploadFileAsync(request.Logo, name, folderName: "business-directory", institutionSlug: currentTenant.InstitutionSlug ?? "");
            }
            if (request.Banner is not null)
            {
                var name = $"{Guid.NewGuid():N}{Path.GetExtension(request.Banner.FileName)}";
                listing.BannerUrl = await storageService.UploadFileAsync(request.Banner, name, folderName: "business-directory", institutionSlug: currentTenant.InstitutionSlug ?? "");
            }

            await listingRepo.AddAsync(listing);
            return listing.ToDto().ToCreatedApiResponse("Business listing submitted for review.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error submitting business listing for member {MemberId}", member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<BusinessListingDto>("Failed to submit business listing");
        }
    }

    public async Task<IApiResponse<BusinessListingDto>> UpdateMyListingAsync(string listingId, UpdateMyBusinessListingRequest request, AuthData member)
    {
        try
        {
            logger.LogInformation("UpdateMyListing request for listingId: {ListingId} by member {MemberId}", listingId, member.Id);
            var listing = await listingRepo.GetByIdAsync(listingId);
            if (listing is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<BusinessListingDto>("Business listing not found");

            if (listing.MemberId != member.Id)
                return ApiResponseExtensions.ToForbiddenApiResponse<BusinessListingDto>("You do not own this business listing.");

            if (listing.Status == "Blacklisted")
                return ApiResponseExtensions.ToBadRequestApiResponse<BusinessListingDto>("This listing is blacklisted and cannot be edited.");

            var contactError = ValidateContact(request.PhoneNumber, request.Email, request.WebsiteUrl);
            if (contactError is not null)
                return ApiResponseExtensions.ToBadRequestApiResponse<BusinessListingDto>(contactError);

            if (string.IsNullOrWhiteSpace(request.BusinessName))
                return ApiResponseExtensions.ToBadRequestApiResponse<BusinessListingDto>("Business name is required.");

            string? logoUrl = null;
            if (request.Logo is not null)
            {
                var name = $"{Guid.NewGuid():N}{Path.GetExtension(request.Logo.FileName)}";
                logoUrl = await storageService.UploadFileAsync(request.Logo, name, folderName: "business-directory", institutionSlug: currentTenant.InstitutionSlug ?? "");
            }
            string? bannerUrl = null;
            if (request.Banner is not null)
            {
                var name = $"{Guid.NewGuid():N}{Path.GetExtension(request.Banner.FileName)}";
                bannerUrl = await storageService.UploadFileAsync(request.Banner, name, folderName: "business-directory", institutionSlug: currentTenant.InstitutionSlug ?? "");
            }

            if (listing.Status == "Approved")
            {
                // Live listing stays untouched — proposed values wait for admin approval.
                listing.PendingChanges = new BusinessListingPendingChanges
                {
                    BusinessName = request.BusinessName,
                    Description = request.Description,
                    LogoUrl = logoUrl ?? listing.LogoUrl,
                    BannerUrl = bannerUrl ?? listing.BannerUrl,
                    Location = request.Location,
                    PhoneNumber = request.PhoneNumber,
                    Email = request.Email,
                    WebsiteUrl = request.WebsiteUrl,
                    ExternalLinkUrl = request.ExternalLinkUrl,
                };
                listing.HasPendingEdit = true;
            }
            else
            {
                // Pending or Rejected — nothing live to protect, overwrite directly.
                listing.BusinessName = request.BusinessName;
                listing.Description = request.Description;
                listing.Location = request.Location;
                listing.PhoneNumber = request.PhoneNumber;
                listing.Email = request.Email;
                listing.WebsiteUrl = request.WebsiteUrl;
                listing.ExternalLinkUrl = request.ExternalLinkUrl;
                if (logoUrl is not null) listing.LogoUrl = logoUrl;
                if (bannerUrl is not null) listing.BannerUrl = bannerUrl;

                if (listing.Status == "Rejected")
                {
                    listing.Status = "Pending";
                    listing.AdminNotes = null;
                }
            }

            listing.UpdatedAt = DateTime.UtcNow;
            listing.UpdatedBy = member.Id;
            await listingRepo.UpdateAsync(listing);

            var message = listing.HasPendingEdit
                ? "Edit submitted for admin approval — your live listing is unchanged until then."
                : "Business listing updated.";
            return listing.ToDto().ToOkApiResponse(message);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating business listing {ListingId} by member {MemberId}", listingId, member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<BusinessListingDto>("Failed to update business listing");
        }
    }

    public async Task<IApiResponse<BusinessListingDto>> HideListingAsync(string listingId, AuthData member)
    {
        try
        {
            var listing = await listingRepo.GetByIdAsync(listingId);
            if (listing is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<BusinessListingDto>("Business listing not found");

            if (listing.MemberId != member.Id)
                return ApiResponseExtensions.ToForbiddenApiResponse<BusinessListingDto>("You do not own this business listing.");

            listing.IsHiddenByMember = true;
            listing.UpdatedAt = DateTime.UtcNow;
            listing.UpdatedBy = member.Id;
            await listingRepo.UpdateAsync(listing);

            return listing.ToDto().ToOkApiResponse("Business listing hidden from the directory.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error hiding business listing {ListingId} by member {MemberId}", listingId, member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<BusinessListingDto>("Failed to hide business listing");
        }
    }

    public async Task<IApiResponse<BusinessListingDto>> UnhideListingAsync(string listingId, AuthData member)
    {
        try
        {
            var listing = await listingRepo.GetByIdAsync(listingId);
            if (listing is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<BusinessListingDto>("Business listing not found");

            if (listing.MemberId != member.Id)
                return ApiResponseExtensions.ToForbiddenApiResponse<BusinessListingDto>("You do not own this business listing.");

            listing.IsHiddenByMember = false;
            listing.UpdatedAt = DateTime.UtcNow;
            listing.UpdatedBy = member.Id;
            await listingRepo.UpdateAsync(listing);

            return listing.ToDto().ToOkApiResponse("Business listing unhidden.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error unhiding business listing {ListingId} by member {MemberId}", listingId, member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<BusinessListingDto>("Failed to unhide business listing");
        }
    }

    public async Task<IApiResponse<object>> DeleteMyListingAsync(string listingId, AuthData member)
    {
        try
        {
            var listing = await listingRepo.GetByIdAsync(listingId);
            if (listing is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Business listing not found");

            if (listing.MemberId != member.Id)
                return ApiResponseExtensions.ToForbiddenApiResponse<object>("You do not own this business listing.");

            if (listing.Status != "Pending")
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Only a pending (not yet reviewed) listing can be withdrawn.");

            await listingRepo.RemoveAsync(listing);
            return new object().ToOkApiResponse("Business listing withdrawn.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error withdrawing business listing {ListingId} by member {MemberId}", listingId, member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to withdraw business listing");
        }
    }

    /// <summary>At least one of phone/email/website must be provided — returns an error message, or null when valid.</summary>
    private static string? ValidateContact(string? phone, string? email, string? website)
    {
        if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(website))
            return "At least one contact method (phone, email, or website) is required.";
        return null;
    }
}
