using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class BusinessDirectoryService(
    IAlumniPgRepository<BusinessListing> listingRepo,
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
                l => string.IsNullOrEmpty(filter.Status) || l.Status == filter.Status);

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
            if (listing is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<BusinessListingDto>("Business listing not found");

            return listing.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving business listing {ListingId}", listingId);
            return ApiResponseExtensions.ToServerErrorApiResponse<BusinessListingDto>("Failed to retrieve business listing");
        }
    }

    public async Task<IApiResponse<BusinessListingDto>> CreateListingAsync(CreateBusinessListingRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("CreateListing request: {Request} by admin {AdminId}", request.Serialize(), admin.Id);

            var contactError = ValidateContact(request.PhoneNumber, request.Email, request.WebsiteUrl);
            if (contactError is not null)
                return ApiResponseExtensions.ToBadRequestApiResponse<BusinessListingDto>(contactError);

            if (string.IsNullOrWhiteSpace(request.BusinessName))
                return ApiResponseExtensions.ToBadRequestApiResponse<BusinessListingDto>("Business name is required.");

            var listing = new BusinessListing
            {
                MemberId = null,
                Member = null,
                BusinessName = request.BusinessName,
                Description = request.Description,
                Location = request.Location,
                PhoneNumber = request.PhoneNumber,
                Email = request.Email,
                WebsiteUrl = request.WebsiteUrl,
                ExternalLinkUrl = request.ExternalLinkUrl,
                Status = "Approved",
                CreatedBy = admin.Id,
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

            logger.LogInformation("Business listing {ListingId} created by admin {AdminId}", listing.Id, admin.Id);
            return listing.ToDto().ToCreatedApiResponse("Business listing created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating business listing: {Request} by admin {AdminId}", request.Serialize(), admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<BusinessListingDto>("Failed to create business listing");
        }
    }

    public async Task<IApiResponse<BusinessListingDto>> ApproveListingAsync(string listingId, AuthData admin)
    {
        try
        {
            var listing = await listingRepo.GetByIdAsync(listingId);
            if (listing is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<BusinessListingDto>("Business listing not found");

            if (listing.Status != "Pending")
                return ApiResponseExtensions.ToBadRequestApiResponse<BusinessListingDto>("Only a pending listing can be approved.");

            listing.Status = "Approved";
            listing.AdminNotes = null;
            listing.UpdatedAt = DateTime.UtcNow;
            listing.UpdatedBy = admin.Id;
            await listingRepo.UpdateAsync(listing);

            return listing.ToDto().ToOkApiResponse("Business listing approved.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error approving business listing {ListingId}", listingId);
            return ApiResponseExtensions.ToServerErrorApiResponse<BusinessListingDto>("Failed to approve business listing");
        }
    }

    public async Task<IApiResponse<BusinessListingDto>> RejectListingAsync(string listingId, string? adminNotes, AuthData admin)
    {
        try
        {
            var listing = await listingRepo.GetByIdAsync(listingId);
            if (listing is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<BusinessListingDto>("Business listing not found");

            if (listing.Status != "Pending")
                return ApiResponseExtensions.ToBadRequestApiResponse<BusinessListingDto>("Only a pending listing can be rejected.");

            listing.Status = "Rejected";
            listing.AdminNotes = adminNotes;
            listing.UpdatedAt = DateTime.UtcNow;
            listing.UpdatedBy = admin.Id;
            await listingRepo.UpdateAsync(listing);

            return listing.ToDto().ToOkApiResponse("Business listing rejected.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error rejecting business listing {ListingId}", listingId);
            return ApiResponseExtensions.ToServerErrorApiResponse<BusinessListingDto>("Failed to reject business listing");
        }
    }

    public async Task<IApiResponse<BusinessListingDto>> ApproveEditAsync(string listingId, AuthData admin)
    {
        try
        {
            var listing = await listingRepo.GetByIdAsync(listingId);
            if (listing is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<BusinessListingDto>("Business listing not found");

            if (!listing.HasPendingEdit || listing.PendingChanges is null)
                return ApiResponseExtensions.ToBadRequestApiResponse<BusinessListingDto>("This listing has no pending edit to approve.");

            var pc = listing.PendingChanges;
            if (pc.BusinessName is not null) listing.BusinessName = pc.BusinessName;
            if (pc.Description is not null) listing.Description = pc.Description;
            if (pc.LogoUrl is not null) listing.LogoUrl = pc.LogoUrl;
            if (pc.BannerUrl is not null) listing.BannerUrl = pc.BannerUrl;
            if (pc.Location is not null) listing.Location = pc.Location;
            if (pc.PhoneNumber is not null) listing.PhoneNumber = pc.PhoneNumber;
            if (pc.Email is not null) listing.Email = pc.Email;
            if (pc.WebsiteUrl is not null) listing.WebsiteUrl = pc.WebsiteUrl;
            if (pc.ExternalLinkUrl is not null) listing.ExternalLinkUrl = pc.ExternalLinkUrl;

            listing.PendingChanges = null;
            listing.HasPendingEdit = false;
            listing.UpdatedAt = DateTime.UtcNow;
            listing.UpdatedBy = admin.Id;
            await listingRepo.UpdateAsync(listing);

            return listing.ToDto().ToOkApiResponse("Pending edit approved and applied.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error approving pending edit for business listing {ListingId}", listingId);
            return ApiResponseExtensions.ToServerErrorApiResponse<BusinessListingDto>("Failed to approve pending edit");
        }
    }

    public async Task<IApiResponse<BusinessListingDto>> RejectEditAsync(string listingId, string? adminNotes, AuthData admin)
    {
        try
        {
            var listing = await listingRepo.GetByIdAsync(listingId);
            if (listing is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<BusinessListingDto>("Business listing not found");

            if (!listing.HasPendingEdit)
                return ApiResponseExtensions.ToBadRequestApiResponse<BusinessListingDto>("This listing has no pending edit to reject.");

            listing.PendingChanges = null;
            listing.HasPendingEdit = false;
            listing.AdminNotes = adminNotes;
            listing.UpdatedAt = DateTime.UtcNow;
            listing.UpdatedBy = admin.Id;
            await listingRepo.UpdateAsync(listing);

            return listing.ToDto().ToOkApiResponse("Pending edit rejected — live listing unchanged.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error rejecting pending edit for business listing {ListingId}", listingId);
            return ApiResponseExtensions.ToServerErrorApiResponse<BusinessListingDto>("Failed to reject pending edit");
        }
    }

    public async Task<IApiResponse<BusinessListingDto>> BlacklistListingAsync(string listingId, AuthData admin)
    {
        try
        {
            var listing = await listingRepo.GetByIdAsync(listingId);
            if (listing is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<BusinessListingDto>("Business listing not found");

            listing.Status = "Blacklisted";
            listing.UpdatedAt = DateTime.UtcNow;
            listing.UpdatedBy = admin.Id;
            await listingRepo.UpdateAsync(listing);

            return listing.ToDto().ToOkApiResponse("Business listing blacklisted.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error blacklisting business listing {ListingId}", listingId);
            return ApiResponseExtensions.ToServerErrorApiResponse<BusinessListingDto>("Failed to blacklist business listing");
        }
    }

    public async Task<IApiResponse<BusinessListingDto>> UnblacklistListingAsync(string listingId, AuthData admin)
    {
        try
        {
            var listing = await listingRepo.GetByIdAsync(listingId);
            if (listing is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<BusinessListingDto>("Business listing not found");

            if (listing.Status != "Blacklisted")
                return ApiResponseExtensions.ToBadRequestApiResponse<BusinessListingDto>("Only a blacklisted listing can be unblacklisted.");

            listing.Status = "Approved";
            listing.UpdatedAt = DateTime.UtcNow;
            listing.UpdatedBy = admin.Id;
            await listingRepo.UpdateAsync(listing);

            return listing.ToDto().ToOkApiResponse("Business listing unblacklisted.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error unblacklisting business listing {ListingId}", listingId);
            return ApiResponseExtensions.ToServerErrorApiResponse<BusinessListingDto>("Failed to unblacklist business listing");
        }
    }

    public async Task<IApiResponse<BusinessListingDto>> UpdateListingAsync(string listingId, UpdateBusinessListingRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("UpdateListing request for listingId: {ListingId} by admin {AdminId}", listingId, admin.Id);
            var listing = await listingRepo.GetByIdAsync(listingId);
            if (listing is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<BusinessListingDto>("Business listing not found");

            var contactError = ValidateContact(request.PhoneNumber, request.Email, request.WebsiteUrl);
            if (contactError is not null)
                return ApiResponseExtensions.ToBadRequestApiResponse<BusinessListingDto>(contactError);

            if (string.IsNullOrWhiteSpace(request.BusinessName))
                return ApiResponseExtensions.ToBadRequestApiResponse<BusinessListingDto>("Business name is required.");

            // Admin's own direct edit bypasses the pending-edit mechanism entirely — overwrites live fields regardless of Status.
            listing.BusinessName = request.BusinessName;
            listing.Description = request.Description;
            listing.Location = request.Location;
            listing.PhoneNumber = request.PhoneNumber;
            listing.Email = request.Email;
            listing.WebsiteUrl = request.WebsiteUrl;
            listing.ExternalLinkUrl = request.ExternalLinkUrl;

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

            listing.UpdatedAt = DateTime.UtcNow;
            listing.UpdatedBy = admin.Id;
            await listingRepo.UpdateAsync(listing);

            logger.LogInformation("Business listing {ListingId} updated by admin {AdminId}", listing.Id, admin.Id);
            return listing.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating business listing {ListingId} by admin {AdminId}", listingId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<BusinessListingDto>("Failed to update business listing");
        }
    }

    public async Task<IApiResponse<object>> DeleteListingAsync(string listingId)
    {
        try
        {
            var listing = await listingRepo.GetByIdAsync(listingId);
            if (listing is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Business listing not found");

            await listingRepo.RemoveAsync(listing);
            logger.LogInformation("Business listing {ListingId} deleted", listingId);
            return new object().ToOkApiResponse("Business listing deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting business listing {ListingId}", listingId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete business listing");
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
