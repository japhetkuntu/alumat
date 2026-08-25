using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using ContributionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Contribution;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

/// <summary>
/// Operates across every institution — every query here deliberately uses
/// <c>IgnoreQueryFilters()</c> on tenant-scoped entities, since this service
/// (unlike the institution/member APIs) is not itself scoped to one tenant.
/// </summary>
public class InstitutionManagementService(
    AlumniDbContext db, IAuditLogService auditLog, IPaystackService paystackService,
    IConfiguration config, ILogger<InstitutionManagementService> logger)
    : IInstitutionManagementService
{
    /// <summary>
    /// The member and institution portals live on entirely different base
    /// domains (e.g. "yourplatform.com" vs "admin.yourplatform.com") — never
    /// derived from each other, always explicit config, so a naming
    /// convention change on one side can't silently break the other.
    /// </summary>
    private string MemberPortalUrl(string slug)
    {
        var domain = config["PlatformBaseDomain"];
        return string.IsNullOrWhiteSpace(domain) ? string.Empty : $"https://{slug}.{domain}";
    }

    private string InstitutionPortalUrl(string slug)
    {
        var domain = config["AdminBaseDomain"];
        return string.IsNullOrWhiteSpace(domain) ? string.Empty : $"https://{slug}.{domain}";
    }
    public async Task<IApiResponse<PgPagedResult<InstitutionListItemResponse>>> GetInstitutionsAsync(
        int page, int pageSize, string? search, string? status)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 20;

        var query = db.Institutions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(i => i.Name.ToLower().Contains(s) || i.Slug.ToLower().Contains(s) || i.ContactEmail.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(i => i.Status == status);
        }

        var totalCount = await query.CountAsync();
        var institutions = await query
            .OrderByDescending(i => i.OnboardedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var planPrices = await db.Plans.ToDictionaryAsync(p => p.Name, p => (p.Price, p.BillingInterval));

        var items = new List<InstitutionListItemResponse>();
        foreach (var i in institutions)
        {
            var memberCount = await db.Set<MemberEntity>().IgnoreQueryFilters().CountAsync(m => m.InstitutionId == i.Id);
            var revenue = await db.Set<ContributionEntity>().IgnoreQueryFilters()
                .Where(c => c.InstitutionId == i.Id && c.Status == "Confirmed")
                .SumAsync(c => c.PlatformFeeAmount);
            items.Add(new InstitutionListItemResponse(
                i.Id, i.Name, i.Slug, i.CustomDomain, i.ContactName, i.ContactEmail,
                i.Plan, i.Status, memberCount, i.MemberLimit, i.OnboardedAt, ResolveMrr(i.Plan, planPrices),
                i.PlatformFeePercentage, revenue, MemberPortalUrl(i.Slug), InstitutionPortalUrl(i.Slug)));
        }

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var result = new PgPagedResult<InstitutionListItemResponse>
        {
            PageIndex = page,
            PageSize = pageSize,
            Count = items.Count,
            TotalCount = totalCount,
            TotalPages = totalPages,
            LowerBoundSize = items.Count == 0 ? 0 : ((page - 1) * pageSize) + 1,
            UpperBoundSize = Math.Min(page * pageSize, totalCount),
            Results = items,
        };

        return result.ToOkApiResponse();
    }

    public async Task<IApiResponse<InstitutionDetailResponse>> GetInstitutionAsync(string id)
    {
        var institution = await db.Institutions.FirstOrDefaultAsync(i => i.Id == id);
        if (institution is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<InstitutionDetailResponse>("Institution not found");

        return (await ToDetailDtoAsync(institution)).ToOkApiResponse();
    }

    public async Task<IApiResponse<InstitutionDetailResponse>> CreateInstitutionAsync(CreateInstitutionRequest request, string createdBy, string actorName)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await db.Institutions.AnyAsync(i => i.Slug == slug))
            return ApiResponseExtensions.ToConflictApiResponse<InstitutionDetailResponse>("Slug is already taken");

        var email = request.AdminEmail.Trim().ToLowerInvariant();
        if (await db.Set<StaffEntity>().IgnoreQueryFilters().AnyAsync(a => a.Email == email))
            return ApiResponseExtensions.ToConflictApiResponse<InstitutionDetailResponse>("An admin with that email already exists");

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var institution = new Institution
            {
                Name = request.Name,
                Slug = slug,
                ContactName = request.ContactName,
                ContactEmail = request.ContactEmail,
                PortalName = string.IsNullOrWhiteSpace(request.PortalName) ? request.Name : request.PortalName,
                SupportEmail = request.SupportEmail,
                PrimaryColorHex = request.PrimaryColorHex,
                SecondaryColorHex = request.SecondaryColorHex,
                Plan = request.Plan,
                Status = "Trial",
                TrialEndsAt = DateTime.UtcNow.AddDays(14),
                OnboardedAt = DateTime.UtcNow,
                PlatformFeePercentage = request.PlatformFeePercentage,
                SettlementBankCode = request.SettlementBankCode,
                SettlementBankName = request.SettlementBankName,
                SettlementAccountNumber = request.SettlementAccountNumber,
                SettlementAccountName = request.SettlementAccountName,
                CreatedBy = createdBy,
            };

            if (!string.IsNullOrWhiteSpace(request.SettlementBankCode) && !string.IsNullOrWhiteSpace(request.SettlementAccountNumber))
            {
                var subaccount = await paystackService.CreateSubaccountAsync(new SubaccountRequest
                {
                    BusinessName = institution.Name,
                    SettlementBank = request.SettlementBankCode,
                    AccountNumber = request.SettlementAccountNumber,
                    PercentageCharge = request.PlatformFeePercentage,
                });
                if (subaccount.Status)
                    institution.PaystackSubaccountCode = subaccount.Data?.SubaccountCode;
                else
                    logger.LogWarning("Paystack subaccount creation failed during onboarding for {Slug}: {Message}", slug, subaccount.Message);
            }

            db.Institutions.Add(institution);
            await db.SaveChangesAsync();

            var admin = new StaffEntity
            {
                InstitutionId = institution.Id,
                FirstName = request.AdminFirstName,
                LastName = request.AdminLastName,
                Email = email,
                Password = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
                Role = "SuperAdmin",
                CreatedBy = createdBy,
            };
            db.Set<StaffEntity>().Add(admin);
            await db.SaveChangesAsync();

            await transaction.CommitAsync();

            logger.LogInformation("Onboarded institution {Slug} ({InstitutionId}) with first admin {AdminEmail}", slug, institution.Id, email);
            await auditLog.LogAsync(createdBy, actorName, "onboarded institution", institution.Name);

            return (await ToDetailDtoAsync(institution)).ToCreatedApiResponse();
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            logger.LogError(e, "Failed to onboard institution {Slug}", slug);
            return ApiResponseExtensions.ToServerErrorApiResponse<InstitutionDetailResponse>("Failed to create institution");
        }
    }

    public async Task<IApiResponse<InstitutionDetailResponse>> UpdateStatusAsync(string id, UpdateInstitutionStatusRequest request, string updatedBy, string actorName)
    {
        var institution = await db.Institutions.FirstOrDefaultAsync(i => i.Id == id);
        if (institution is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<InstitutionDetailResponse>("Institution not found");

        institution.Status = request.Status;
        institution.UpdatedAt = DateTime.UtcNow;
        institution.UpdatedBy = updatedBy;
        await db.SaveChangesAsync();

        await auditLog.LogAsync(updatedBy, actorName, $"set institution status to {request.Status}", institution.Name);

        return (await ToDetailDtoAsync(institution)).ToOkApiResponse("Status updated");
    }

    public async Task<IApiResponse<InstitutionDetailResponse>> UpdatePlanAsync(string id, UpdateInstitutionPlanRequest request, string updatedBy, string actorName)
    {
        var institution = await db.Institutions.FirstOrDefaultAsync(i => i.Id == id);
        if (institution is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<InstitutionDetailResponse>("Institution not found");

        institution.Plan = request.Plan;
        if (request.MemberLimit.HasValue) institution.MemberLimit = request.MemberLimit.Value;
        if (request.StorageLimitGb.HasValue) institution.StorageLimitGb = request.StorageLimitGb.Value;
        institution.UpdatedAt = DateTime.UtcNow;
        institution.UpdatedBy = updatedBy;
        await db.SaveChangesAsync();

        await auditLog.LogAsync(updatedBy, actorName, $"changed institution plan to {request.Plan}", institution.Name);

        return (await ToDetailDtoAsync(institution)).ToOkApiResponse("Plan updated");
    }

    public async Task<IApiResponse<InstitutionDetailResponse>> UpdateBrandingAsync(string id, UpdateInstitutionBrandingRequest request, string updatedBy, string actorName)
    {
        var institution = await db.Institutions.FirstOrDefaultAsync(i => i.Id == id);
        if (institution is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<InstitutionDetailResponse>("Institution not found");

        institution.PortalName = request.PortalName;
        institution.Tagline = request.Tagline;
        institution.ContactEmail = request.ContactEmail;
        institution.SupportEmail = request.SupportEmail;
        institution.LogoUrl = request.LogoUrl;
        institution.IconUrl = request.IconUrl;
        institution.PrimaryColorHex = request.PrimaryColorHex;
        institution.SecondaryColorHex = request.SecondaryColorHex;
        institution.InstitutionPortalTitle = request.InstitutionPortalTitle;
        institution.InstitutionAuthHeadline = request.InstitutionAuthHeadline;
        institution.InstitutionAuthSubtext = request.InstitutionAuthSubtext;
        institution.MemberPortalTitle = request.MemberPortalTitle;
        institution.MemberAuthHeadline = request.MemberAuthHeadline;
        institution.MemberAuthSubtext = request.MemberAuthSubtext;
        institution.RequireStudentId = request.RequireStudentId;
        institution.UpdatedAt = DateTime.UtcNow;
        institution.UpdatedBy = updatedBy;
        await db.SaveChangesAsync();

        await auditLog.LogAsync(updatedBy, actorName, "updated institution branding", institution.Name);

        return (await ToDetailDtoAsync(institution)).ToOkApiResponse("Branding updated");
    }

    public async Task<IApiResponse<InstitutionDetailResponse>> UpdateFeaturesAsync(string id, UpdateInstitutionFeaturesRequest request, string updatedBy, string actorName)
    {
        var institution = await db.Institutions.FirstOrDefaultAsync(i => i.Id == id);
        if (institution is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<InstitutionDetailResponse>("Institution not found");

        var unknown = request.DisabledFeatures.Except(InstitutionFeatures.All).ToList();
        if (unknown.Count > 0)
            return ApiResponseExtensions.ToBadRequestApiResponse<InstitutionDetailResponse>($"Unknown feature key(s): {string.Join(", ", unknown)}");

        institution.DisabledFeatures = request.DisabledFeatures.Distinct().ToList();
        institution.UpdatedAt = DateTime.UtcNow;
        institution.UpdatedBy = updatedBy;
        await db.SaveChangesAsync();

        var summary = institution.DisabledFeatures.Count == 0 ? "all features enabled" : $"disabled: {string.Join(", ", institution.DisabledFeatures)}";
        await auditLog.LogAsync(updatedBy, actorName, $"updated institution features ({summary})", institution.Name);

        return (await ToDetailDtoAsync(institution)).ToOkApiResponse("Features updated");
    }

    public async Task<IApiResponse<InstitutionDetailResponse>> UpdatePaymentsAsync(string id, UpdateInstitutionPaymentsRequest request, string updatedBy, string actorName)
    {
        var institution = await db.Institutions.FirstOrDefaultAsync(i => i.Id == id);
        if (institution is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<InstitutionDetailResponse>("Institution not found");

        var subaccountRequest = new SubaccountRequest
        {
            BusinessName = institution.Name,
            SettlementBank = request.SettlementBankCode,
            AccountNumber = request.SettlementAccountNumber,
            PercentageCharge = request.PlatformFeePercentage,
        };

        // A subaccount code stored locally can go stale if someone deletes it
        // directly in the Paystack dashboard (as opposed to through this
        // app) — an update call against a deleted code fails instead of
        // syncing anything, so verify it still exists on Paystack first and
        // fall back to creating a fresh one if it doesn't.
        var existingCode = institution.PaystackSubaccountCode;
        if (!string.IsNullOrWhiteSpace(existingCode))
        {
            var existing = await paystackService.FetchSubaccountAsync(existingCode);
            if (!existing.Status || existing.Data is null)
            {
                logger.LogWarning(
                    "Institution {InstitutionId}'s stored Paystack subaccount {SubaccountCode} is no longer valid ({Message}) — creating a new one instead of updating",
                    institution.Id, existingCode, existing.Message);
                existingCode = null;
            }
        }

        var subaccount = string.IsNullOrWhiteSpace(existingCode)
            ? await paystackService.CreateSubaccountAsync(subaccountRequest)
            : await paystackService.UpdateSubaccountAsync(existingCode, subaccountRequest);

        if (!subaccount.Status)
            return ApiResponseExtensions.ToBadRequestApiResponse<InstitutionDetailResponse>($"Paystack subaccount sync failed: {subaccount.Message}");

        institution.PlatformFeePercentage = request.PlatformFeePercentage;
        institution.SettlementBankCode = request.SettlementBankCode;
        institution.SettlementBankName = request.SettlementBankName;
        institution.SettlementAccountNumber = request.SettlementAccountNumber;
        institution.SettlementAccountName = request.SettlementAccountName;
        if (!string.IsNullOrWhiteSpace(subaccount.Data?.SubaccountCode))
            institution.PaystackSubaccountCode = subaccount.Data.SubaccountCode;
        institution.UpdatedAt = DateTime.UtcNow;
        institution.UpdatedBy = updatedBy;
        await db.SaveChangesAsync();

        await auditLog.LogAsync(updatedBy, actorName, $"updated institution payment settings (fee {request.PlatformFeePercentage}%)", institution.Name);

        return (await ToDetailDtoAsync(institution)).ToOkApiResponse("Payment settings updated");
    }

    public async Task<IApiResponse<InstitutionRevenueResponse>> GetRevenueAsync(string id)
    {
        var institution = await db.Institutions.FirstOrDefaultAsync(i => i.Id == id);
        if (institution is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<InstitutionRevenueResponse>("Institution not found");

        var confirmed = await db.Set<ContributionEntity>().IgnoreQueryFilters()
            .Where(c => c.InstitutionId == id && c.Status == "Confirmed")
            .ToListAsync();

        var gross = confirmed.Sum(c => c.Amount);
        var fee = confirmed.Sum(c => c.PlatformFeeAmount);
        var net = confirmed.Sum(c => c.NetAmountToInstitution);

        return new InstitutionRevenueResponse(id, gross, fee, net, confirmed.Count).ToOkApiResponse();
    }

    public async Task<IApiResponse<InstitutionDetailResponse>> UpdateLandingContentAsync(string id, UpdateInstitutionLandingContentRequest request, string updatedBy, string actorName)
    {
        var institution = await db.Institutions.FirstOrDefaultAsync(i => i.Id == id);
        if (institution is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<InstitutionDetailResponse>("Institution not found");

        institution.LandingPageStories = request.LandingPageStories;
        institution.NewsBanner = request.NewsBanner;
        institution.UpdatedAt = DateTime.UtcNow;
        institution.UpdatedBy = updatedBy;
        await db.SaveChangesAsync();

        await auditLog.LogAsync(updatedBy, actorName, "updated institution landing page content", institution.Name);

        return (await ToDetailDtoAsync(institution)).ToOkApiResponse("Landing content updated");
    }

    public BaseDomainsResponse GetBaseDomains() =>
        new(config["PlatformBaseDomain"] ?? string.Empty, config["AdminBaseDomain"] ?? string.Empty);

    public async Task<IApiResponse<SlugAvailabilityResponse>> CheckSlugAsync(string slug)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        var taken = await db.Institutions.AnyAsync(i => i.Slug == normalized);
        return new SlugAvailabilityResponse(normalized, !taken).ToOkApiResponse();
    }

    public async Task<IApiResponse<PlatformDashboardSummary>> GetDashboardSummaryAsync()
    {
        var totalInstitutions = await db.Institutions.CountAsync();
        var activeCount = await db.Institutions.CountAsync(i => i.Status == "Active");
        var trialCount = await db.Institutions.CountAsync(i => i.Status == "Trial");
        var totalMembers = await db.Set<MemberEntity>().IgnoreQueryFilters().CountAsync();
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var newThisMonth = await db.Institutions.CountAsync(i => i.OnboardedAt >= monthStart);

        var mrr = await GetMrrAsync();
        var revenue = await db.Set<ContributionEntity>().IgnoreQueryFilters()
            .Where(c => c.Status == "Confirmed")
            .SumAsync(c => c.PlatformFeeAmount);

        var growthCounts = new List<int>();
        var growthLabels = new List<string>();
        for (var offset = 5; offset >= 0; offset--)
        {
            var bucketStart = monthStart.AddMonths(-offset);
            var bucketEnd = bucketStart.AddMonths(1);
            growthCounts.Add(await db.Institutions.CountAsync(i => i.OnboardedAt < bucketEnd));
            growthLabels.Add(bucketStart.ToString("MMM"));
        }

        return new PlatformDashboardSummary(totalInstitutions, activeCount, trialCount, totalMembers, newThisMonth, mrr, revenue, growthCounts, growthLabels)
            .ToOkApiResponse();
    }

    private async Task<decimal> GetMrrAsync()
    {
        var institutionPlans = await db.Institutions.Where(i => i.Status == "Active").Select(i => i.Plan).ToListAsync();
        var planPrices = await db.Plans.ToDictionaryAsync(p => p.Name, p => (p.Price, p.BillingInterval));
        return institutionPlans.Sum(planName => ResolveMrr(planName, planPrices));
    }

    private static decimal ResolveMrr(string planName, Dictionary<string, (decimal? Price, string BillingInterval)> planPrices)
    {
        if (!planPrices.TryGetValue(planName, out var plan) || plan.Price is null) return 0;
        return plan.BillingInterval == "annual" ? plan.Price.Value / 12m : plan.Price.Value;
    }

    private async Task<InstitutionDetailResponse> ToDetailDtoAsync(Institution i)
    {
        var memberCount = await db.Set<MemberEntity>().IgnoreQueryFilters().CountAsync(m => m.InstitutionId == i.Id);
        var planPrices = await db.Plans.ToDictionaryAsync(p => p.Name, p => (p.Price, p.BillingInterval));
        var revenue = await db.Set<ContributionEntity>().IgnoreQueryFilters()
            .Where(c => c.InstitutionId == i.Id && c.Status == "Confirmed")
            .SumAsync(c => c.PlatformFeeAmount);
        return new InstitutionDetailResponse(
            i.Id, i.Name, i.Slug, i.CustomDomain,
            i.PortalName, i.Tagline, i.ContactName, i.ContactEmail, i.SupportEmail,
            i.LogoUrl, i.IconUrl, i.PrimaryColorHex, i.SecondaryColorHex,
            i.InstitutionPortalTitle, i.InstitutionAuthHeadline, i.InstitutionAuthSubtext,
            i.MemberPortalTitle, i.MemberAuthHeadline, i.MemberAuthSubtext,
            i.RequireStudentId, i.DisabledFeatures, i.LandingPageStories, i.NewsBanner,
            i.Plan, i.Status, memberCount, i.MemberLimit, 0, i.StorageLimitGb,
            i.OnboardedAt, i.TrialEndsAt, ResolveMrr(i.Plan, planPrices),
            i.PlatformFeePercentage, i.PaystackSubaccountCode,
            i.SettlementBankCode, i.SettlementBankName,
            i.SettlementAccountNumber, i.SettlementAccountName, revenue,
            MemberPortalUrl(i.Slug), InstitutionPortalUrl(i.Slug));
    }
}
