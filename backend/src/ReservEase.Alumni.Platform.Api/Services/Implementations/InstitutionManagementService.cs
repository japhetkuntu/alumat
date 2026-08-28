using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.Platform.Api.Actors;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using Microsoft.Extensions.Options;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using ContributionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Contribution;
using StoreOrderEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.StoreOrder;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

/// <summary>
/// Operates across every institution — every query here deliberately uses
/// <c>IgnoreQueryFilters()</c> on tenant-scoped entities, since this service
/// (unlike the institution/member APIs) is not itself scoped to one tenant.
/// </summary>
public class InstitutionManagementService(
    AlumniDbContext db, IAuditLogService auditLog, IPaystackService paystackService,
    IConfiguration config, INotificationActor notificationActor, IOptions<MailtrapConfig> mailtrapConfigOptions,
    ILogger<InstitutionManagementService> logger)
    : IInstitutionManagementService
{
    private readonly MailtrapConfig mailtrapConfig = mailtrapConfigOptions.Value;
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

        var items = new List<InstitutionListItemResponse>();
        foreach (var i in institutions)
        {
            var memberCount = await db.Set<MemberEntity>().IgnoreQueryFilters().CountAsync(m => m.InstitutionId == i.Id);
            // Zero-Deduction model: our actual revenue is PlatformRevenueAmount
            // (collected from the payer's grossed-up charge), not PlatformFeeAmount
            // (which is always 0 for online payments now — nothing is deducted
            // from the institution).
            var revenue = await db.Set<ContributionEntity>().IgnoreQueryFilters()
                .Where(c => c.InstitutionId == i.Id && c.Status == "Confirmed")
                .SumAsync(c => c.PlatformRevenueAmount);
            items.Add(new InstitutionListItemResponse(
                i.Id, i.Name, i.Slug, i.CustomDomain, i.ContactName, i.ContactEmail,
                i.LogoUrl, i.Status, memberCount, i.OnboardedAt,
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

        if (request.MemberActivePolicy != MembershipActivityCalculator.ApprovedOnlyPolicy
            && request.MemberActivePolicy != MembershipActivityCalculator.DuesRequiredPolicy)
            return ApiResponseExtensions.ToBadRequestApiResponse<InstitutionDetailResponse>("MemberActivePolicy must be either \"ApprovedOnly\" or \"DuesRequired\"");

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
                MemberActivePolicy = request.MemberActivePolicy,
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
                    // Zero-Deduction model: every payment now sends an explicit
                    // transaction_charge computed per-transaction from
                    // PlatformFeePercentage (see ContributionService), which
                    // overrides this subaccount-level default. Leaving this at 0
                    // prevents a double-deduction if any future code path ever
                    // initializes a payment without setting transaction_charge.
                    PercentageCharge = 0,
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

    public async Task<IApiResponse<InstitutionDetailResponse>> UpdateMemberActivePolicyAsync(string id, UpdateInstitutionMemberPolicyRequest request, string updatedBy, string actorName)
    {
        var institution = await db.Institutions.FirstOrDefaultAsync(i => i.Id == id);
        if (institution is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<InstitutionDetailResponse>("Institution not found");

        if (request.MemberActivePolicy != MembershipActivityCalculator.ApprovedOnlyPolicy
            && request.MemberActivePolicy != MembershipActivityCalculator.DuesRequiredPolicy)
            return ApiResponseExtensions.ToBadRequestApiResponse<InstitutionDetailResponse>("MemberActivePolicy must be either \"ApprovedOnly\" or \"DuesRequired\"");

        institution.MemberActivePolicy = request.MemberActivePolicy;
        institution.UpdatedAt = DateTime.UtcNow;
        institution.UpdatedBy = updatedBy;
        await db.SaveChangesAsync();

        await auditLog.LogAsync(updatedBy, actorName, $"set active-member policy to {request.MemberActivePolicy}", institution.Name);

        return (await ToDetailDtoAsync(institution)).ToOkApiResponse("Active-member policy updated");
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
            // Zero-Deduction model: kept at 0 — see CreateInstitutionAsync's
            // matching comment. The real split is computed per-transaction.
            PercentageCharge = 0,
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
        // Zero-Deduction model: our actual revenue is PlatformRevenueAmount; net
        // to the institution is always equal to gross now (nothing deducted).
        var fee = confirmed.Sum(c => c.PlatformRevenueAmount);
        var net = confirmed.Sum(c => c.NetAmountToInstitution);

        return new InstitutionRevenueResponse(id, gross, fee, net, confirmed.Count).ToOkApiResponse();
    }

    /// <summary>
    /// Every payment an institution has ever collected — Contributions and
    /// Store orders, every status — so platform support can see the full
    /// picture when helping troubleshoot, not just the confirmed-and-settled
    /// revenue figure GetRevenueAsync reports.
    /// </summary>
    public async Task<IApiResponse<PgPagedResult<PlatformPaymentDto>>> GetPaymentsAsync(string? id, int page, int pageSize, string? status, string? source)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 20;

        var payments = new List<PlatformPaymentDto>();

        if (string.IsNullOrEmpty(source) || source == "Contribution")
        {
            var contributions = await db.Set<ContributionEntity>().IgnoreQueryFilters()
                .Where(c => string.IsNullOrEmpty(id) || c.InstitutionId == id)
                .ToListAsync();
            payments.AddRange(contributions.Select(c => new PlatformPaymentDto(
                c.Id, "Contribution", c.InstitutionId,
                c.Member is null ? null : $"{c.Member.FirstName} {c.Member.LastName}", c.Member?.Email,
                c.Campaign?.Title ?? "Contribution", c.Amount, c.Status, c.PaymentMethod, c.TransactionRef,
                c.CreatedAt, c.ConfirmedAt)));
        }

        if (string.IsNullOrEmpty(source) || source == "StoreOrder")
        {
            var orders = await db.Set<StoreOrderEntity>().IgnoreQueryFilters()
                .Where(o => string.IsNullOrEmpty(id) || o.InstitutionId == id)
                .ToListAsync();
            payments.AddRange(orders.Select(o => new PlatformPaymentDto(
                o.Id, "StoreOrder", o.InstitutionId,
                o.Member is null ? null : $"{o.Member.FirstName} {o.Member.LastName}", o.Member?.Email,
                o.Items.Count == 1 ? o.Items[0].ProductName : $"{o.Items.Count} items",
                o.TotalAmount, o.Status, o.PaymentMethod, o.TransactionRef,
                o.CreatedAt, o.ConfirmedAt)));
        }

        if (!string.IsNullOrEmpty(status))
            payments = payments.Where(p => p.Status == status).ToList();

        var ordered = payments.OrderByDescending(p => p.CreatedAt).ToList();
        var totalCount = ordered.Count;
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var result = new PgPagedResult<PlatformPaymentDto>
        {
            PageIndex = page,
            PageSize = pageSize,
            Count = pageItems.Count,
            TotalCount = totalCount,
            TotalPages = totalPages,
            LowerBoundSize = pageItems.Count == 0 ? 0 : ((page - 1) * pageSize) + 1,
            UpperBoundSize = Math.Min(page * pageSize, totalCount),
            Results = pageItems,
        };
        return result.ToOkApiResponse();
    }

    public async Task<IApiResponse<InstitutionDetailResponse>> UpdateLandingContentAsync(string id, UpdateInstitutionLandingContentRequest request, string updatedBy, string actorName)
    {
        var institution = await db.Institutions.FirstOrDefaultAsync(i => i.Id == id);
        if (institution is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<InstitutionDetailResponse>("Institution not found");

        institution.LandingPageStories = request.LandingPageStories;
        institution.NewsBanner = request.NewsBanner;
        institution.HeroImageUrls = request.HeroImageUrls;
        institution.HeroHeadline = request.HeroHeadline;
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

        var revenue = await db.Set<ContributionEntity>().IgnoreQueryFilters()
            .Where(c => c.Status == "Confirmed")
            .SumAsync(c => c.PlatformRevenueAmount);

        var growthCounts = new List<int>();
        var growthLabels = new List<string>();
        for (var offset = 5; offset >= 0; offset--)
        {
            var bucketStart = monthStart.AddMonths(-offset);
            var bucketEnd = bucketStart.AddMonths(1);
            growthCounts.Add(await db.Institutions.CountAsync(i => i.OnboardedAt < bucketEnd));
            growthLabels.Add(bucketStart.ToString("MMM"));
        }

        return new PlatformDashboardSummary(totalInstitutions, activeCount, trialCount, totalMembers, newThisMonth, revenue, growthCounts, growthLabels)
            .ToOkApiResponse();
    }

    public async Task<IApiResponse<List<InstitutionStaffDto>>> GetInstitutionStaffAsync(string institutionId)
    {
        var staff = await db.Set<StaffEntity>().IgnoreQueryFilters()
            .Where(s => s.InstitutionId == institutionId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return staff
            .Select(s => new InstitutionStaffDto(s.Id, s.FirstName, s.LastName, s.Email, s.Role, s.IsDisabled, s.LastLoginAt, s.CreatedAt))
            .ToList()
            .ToOkApiResponse();
    }

    public async Task<IApiResponse<InstitutionStaffDto>> InviteInstitutionStaffAsync(
        string institutionId, InviteInstitutionStaffRequest request, string createdBy, string actorName)
    {
        var institution = await db.Institutions.FirstOrDefaultAsync(i => i.Id == institutionId);
        if (institution is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<InstitutionStaffDto>("Institution not found");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Set<StaffEntity>().IgnoreQueryFilters().AnyAsync(s => s.Email == email && s.InstitutionId == institutionId))
            return ApiResponseExtensions.ToConflictApiResponse<InstitutionStaffDto>("An admin with that email already exists");

        // No password is ever set or transmitted here — the invitee gets a
        // reset-password link (same token mechanism as "forgot password")
        // and chooses their own password on first sign-in. The random hash
        // below is never usable to log in; it exists only because Password
        // is a required, non-nullable column.
        var staff = new StaffEntity
        {
            InstitutionId = institutionId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
            Role = request.Role,
            PasswordResetToken = $"reset_{Guid.NewGuid():N}",
            PasswordResetSentAt = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
        db.Set<StaffEntity>().Add(staff);
        await db.SaveChangesAsync();

        var institutionDisplayName = string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName;
        SendStaffInviteEmailAsync(
            staff.FirstName, staff.Email, staff.PasswordResetToken, institutionDisplayName, InstitutionPortalUrl(institution.Slug),
            institution.PrimaryColorHex, institution.SecondaryColorHex, institution.LogoUrl);

        logger.LogInformation("Invited staff {Email} to institution {InstitutionId}", email, institutionId);
        await auditLog.LogAsync(createdBy, actorName, $"invited admin {email}", institution.Name);

        return new InstitutionStaffDto(staff.Id, staff.FirstName, staff.LastName, staff.Email, staff.Role, staff.IsDisabled, staff.LastLoginAt, staff.CreatedAt)
            .ToCreatedApiResponse();
    }

    public async Task<IApiResponse<InstitutionStaffDto>> SetInstitutionStaffDisabledAsync(
        string institutionId, string staffId, bool isDisabled, string updatedBy, string actorName)
    {
        var staff = await db.Set<StaffEntity>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == staffId && s.InstitutionId == institutionId);
        if (staff is null)
            return ApiResponseExtensions.ToNotFoundApiResponse<InstitutionStaffDto>("Admin not found");

        staff.IsDisabled = isDisabled;
        staff.UpdatedAt = DateTime.UtcNow;
        staff.UpdatedBy = updatedBy;
        await db.SaveChangesAsync();

        var institution = await db.Institutions.FirstOrDefaultAsync(i => i.Id == institutionId);
        await auditLog.LogAsync(updatedBy, actorName, $"{(isDisabled ? "disabled" : "re-enabled")} admin {staff.Email}", institution?.Name ?? institutionId);

        return new InstitutionStaffDto(staff.Id, staff.FirstName, staff.LastName, staff.Email, staff.Role, staff.IsDisabled, staff.LastLoginAt, staff.CreatedAt)
            .ToOkApiResponse();
    }

    private void SendStaffInviteEmailAsync(
        string firstName, string email, string token, string institutionName, string portalUrl,
        string? primaryColor, string? secondaryColor, string? logoUrl)
    {
        var baseUrl = string.IsNullOrWhiteSpace(portalUrl) ? "https://example.com" : portalUrl;
        var link = $"{baseUrl}/reset-password?token={token}&email={Uri.EscapeDataString(email)}";
        notificationActor.Tell(new SendEmailCommand(
            new SendEmailRequest
            {
                To = [new EmailContact { Email = email, Name = firstName }],
                TemplateId = string.IsNullOrWhiteSpace(mailtrapConfig.Templates.ResetPassword)
                    ? "reset-password"
                    : mailtrapConfig.Templates.ResetPassword,
                TemplateVariables = new
                {
                    first_name = firstName, reset_url = link, brand_name = institutionName,
                    brand_color = primaryColor, brand_secondary_color = secondaryColor, brand_logo = logoUrl,
                },
            },
            $"staff invite email to {email}"));
    }

    private async Task<InstitutionDetailResponse> ToDetailDtoAsync(Institution i)
    {
        var memberCount = await db.Set<MemberEntity>().IgnoreQueryFilters().CountAsync(m => m.InstitutionId == i.Id);
        var revenue = await db.Set<ContributionEntity>().IgnoreQueryFilters()
            .Where(c => c.InstitutionId == i.Id && c.Status == "Confirmed")
            .SumAsync(c => c.PlatformRevenueAmount);
        return new InstitutionDetailResponse(
            i.Id, i.Name, i.Slug, i.CustomDomain,
            i.PortalName, i.Tagline, i.ContactName, i.ContactEmail, i.SupportEmail,
            i.LogoUrl, i.IconUrl, i.PrimaryColorHex, i.SecondaryColorHex,
            i.InstitutionPortalTitle, i.InstitutionAuthHeadline, i.InstitutionAuthSubtext,
            i.MemberPortalTitle, i.MemberAuthHeadline, i.MemberAuthSubtext,
            i.RequireStudentId, i.MemberActivePolicy, i.DisabledFeatures, i.LandingPageStories, i.NewsBanner,
            i.HeroImageUrls, i.HeroHeadline,
            i.Status, memberCount,
            i.OnboardedAt, i.TrialEndsAt,
            i.PlatformFeePercentage, i.PaystackSubaccountCode,
            i.SettlementBankCode, i.SettlementBankName,
            i.SettlementAccountNumber, i.SettlementAccountName, revenue,
            MemberPortalUrl(i.Slug), InstitutionPortalUrl(i.Slug));
    }
}
