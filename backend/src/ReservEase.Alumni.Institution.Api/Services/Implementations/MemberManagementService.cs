using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class MemberManagementService(
    IAlumniPgRepository<Member> memberRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<InstitutionEntity> institutionRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    ICurrentTenantService currentTenant,
    IConfiguration config,
    IOptions<MailtrapConfig> mailtrapConfigOptions,
    ITemporalClientProvider temporalProvider,
    IInstitutionAuditLogService auditLog,
    ILogger<MemberManagementService> logger) : IMemberManagementService
{
    private const int MaxRejections = 3;
    private readonly MailtrapConfig mailtrapConfig = mailtrapConfigOptions.Value;

    private static string GenerateUrlToken(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    /// <summary>
    /// The one channel guaranteed to reach a member regardless of whether
    /// their new status lets them log in — a rejected/blocked/banned member
    /// can't see an in-app notification (see ProcessMemberStatusChangedAsync,
    /// which only writes one for "Active"), so every status transition gets
    /// an email here in addition to that in-app/SMS fan-out.
    /// </summary>
    private async Task SendMemberStatusEmailAsync(Member member, string title, string body, string actionLabel)
    {
        if (string.IsNullOrEmpty(currentTenant.InstitutionId)) return;
        var institution = await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
        if (institution is null) return;

        var memberBaseDomain = config["MemberPortalBaseDomain"];
        var memberPortalUrl = string.IsNullOrWhiteSpace(memberBaseDomain) ? string.Empty : $"https://{institution.Slug}.{memberBaseDomain}";
        var brandName = string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName;

        await temporalProvider.EnqueueNotificationAsync(NotificationRequest.Email(
            new SendEmailRequest
            {
                To = [new EmailContact { Email = member.Email, Name = member.FirstName }],
                TemplateId = "notification",
                TemplateVariables = new
                {
                    first_name = member.FirstName,
                    title,
                    body,
                    badge_label = "Account Update",
                    action_url = memberPortalUrl,
                    action_label = actionLabel,
                    brand_name = brandName,
                    brand_color = institution.PrimaryColorHex,
                    brand_secondary_color = institution.SecondaryColorHex,
                    brand_logo = institution.LogoUrl,
                },
            },
            $"member status change email to {member.Email}"), logger);
    }

    /// <summary>
    /// A newly bulk-created member has no usable password yet — mirrors the
    /// same secure reset-token pattern used for institution admins and staff
    /// invites (InstitutionManagementService/InstitutionAuthService), reusing
    /// Member's own EmailVerificationToken/EmailVerificationSentAt fields
    /// exactly the way MemberAuthService.ForgotPasswordAsync already does, so
    /// the existing Member.Api /reset-password flow validates it unchanged.
    /// The link points at the Member Portal (this institution's own
    /// subdomain), never at the Institution Portal host this request landed
    /// on.
    /// </summary>
    private async Task SendMemberWelcomeEmailAsync(Member member, InstitutionEntity institution)
    {
        var memberBaseDomain = config["MemberPortalBaseDomain"];
        if (string.IsNullOrWhiteSpace(memberBaseDomain)) return;

        var memberPortalUrl = $"https://{institution.Slug}.{memberBaseDomain}";
        var link = $"{memberPortalUrl}/reset-password?token={member.EmailVerificationToken}&email={Uri.EscapeDataString(member.Email)}";
        var brandName = string.IsNullOrWhiteSpace(institution.PortalName) ? institution.Name : institution.PortalName;

        await temporalProvider.EnqueueNotificationAsync(NotificationRequest.Email(
            new SendEmailRequest
            {
                To = [new EmailContact { Email = member.Email, Name = member.FirstName }],
                TemplateId = string.IsNullOrWhiteSpace(mailtrapConfig.Templates.MemberWelcome)
                    ? "member-welcome"
                    : mailtrapConfig.Templates.MemberWelcome,
                TemplateVariables = new
                {
                    first_name = member.FirstName,
                    set_password_url = link,
                    member_portal_url = memberPortalUrl,
                    brand_name = brandName,
                    brand_color = institution.PrimaryColorHex,
                    brand_secondary_color = institution.SecondaryColorHex,
                    brand_logo = institution.LogoUrl,
                },
            },
            $"member welcome email to {member.Email}"), logger);
    }

    /// <summary>
    /// A scoped admin can see/act on a member who's in their batch (graduation
    /// year) OR an approved member of one of their assigned communities —
    /// Member carries no CommunityId of its own, so the community check needs
    /// a CommunityMembership lookup rather than the sync CanViewScopedItem/
    /// CanModifyScopedItem helpers used elsewhere.
    /// </summary>
    private async Task<bool> IsMemberInScopeAsync(AuthData admin, Member member)
    {
        if (admin.Role != StaffRoles.ScopedAdmin)
            return true;

        if (admin.YearGroups?.Contains(member.GraduationYear) == true)
            return true;

        var communityIds = admin.CommunityIds ?? new List<string>();
        if (communityIds.Count == 0)
            return false;

        var membership = await membershipRepo.GetOneAsync(cm =>
            cm.MemberId == member.Id && communityIds.Contains(cm.CommunityId) && cm.Status == "Approved");
        return membership is not null;
    }

    /// <summary>
    /// Member numbers are prefixed by the current institution's own slug
    /// (e.g. "GREENFIELD-2026-0001"), not a fixed string — this platform
    /// hosts any number of institutions, so the prefix has to identify
    /// which one a member number belongs to. Community-type institutions
    /// have no meaningful graduation year (see registration's int?
    /// GraduationYear ?? 0 sentinel), so their member numbers drop the year
    /// segment entirely rather than showing a "-0-" artifact.
    /// </summary>
    private async Task<string> GetMemberNumberPrefixAsync(int? graduationYear)
    {
        string slug = "MEMBER";
        var isCommunity = false;
        if (!string.IsNullOrEmpty(currentTenant.InstitutionId))
        {
            var institution = await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
            if (!string.IsNullOrWhiteSpace(institution?.Slug))
                slug = institution.Slug.ToUpperInvariant();
            isCommunity = institution?.OrganizationType == OrganizationTypes.Community;
        }
        return isCommunity ? $"{slug}-" : $"{slug}-{graduationYear}-";
    }

    public async Task<IApiResponse<PgPagedResult<MemberListItem>>> GetMembersAsync(MemberListFilter filter, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetMembers request with filter: {Filter} (admin: {AdminId})", filter.Serialize(), admin.Id);

            var isSuper = admin.Role != StaffRoles.ScopedAdmin;
            var yearGroups = admin.YearGroups ?? new List<int>();
            List<string>? communityMemberIds = null;
            if (!isSuper)
            {
                var communityIds = admin.CommunityIds ?? new List<string>();
                communityMemberIds = communityIds.Count == 0
                    ? new List<string>()
                    : (await membershipRepo.GetAllAsync(m => communityIds.Contains(m.CommunityId) && m.Status == "Approved"))
                        .Select(m => m.MemberId).Distinct().ToList();
            }

            var search = filter.Search is null ? null : string.Join(" ", filter.Search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLower();
            var jobTitleContains = filter.JobTitleContains?.ToLower();
            var locationContains = filter.LocationContains?.ToLower();
            var result = await memberRepo.GetPagedAsync(
                filter.Page, filter.PageSize,
                sortColumn: filter.SortColumn ?? "CreatedAt", sortDir: filter.SortDir ?? "desc",
                f => (isSuper || yearGroups.Contains(f.GraduationYear) || communityMemberIds!.Contains(f.Id))
                  && (string.IsNullOrEmpty(filter.Status) ? f.Status != "Deleted" : f.Status == filter.Status)
                  && (string.IsNullOrEmpty(filter.DepartmentId) || f.DepartmentId == filter.DepartmentId)
                  && (!filter.GraduationYearFrom.HasValue || f.GraduationYear >= filter.GraduationYearFrom.Value)
                  && (!filter.GraduationYearTo.HasValue || f.GraduationYear <= filter.GraduationYearTo.Value)
                  && (string.IsNullOrEmpty(jobTitleContains) || (f.JobTitle != null && f.JobTitle.ToLower().Contains(jobTitleContains)))
                  && (string.IsNullOrEmpty(locationContains) || (f.Location != null && f.Location.ToLower().Contains(locationContains)))
                  && TextSearch.Matches(search, f.FirstName, f.LastName, (f.FirstName + " " + f.LastName), (f.LastName + " " + f.FirstName), f.MemberNumber, f.Email));

            var items = result.Results.Select(m => new MemberListItem(
                m.Id, m.FirstName, m.LastName, m.Email, m.Phone,
                m.GraduationYear, m.DepartmentId, m.Status, m.Company, m.CreatedAt,
                m.MemberNumber, m.IsEmailVerified, m.RejectionCount, m.ProfilePictureUrl,
                m.IsMembershipActive, m.MembershipExpiry, m.JobTitle, m.Location));

            return new PgPagedResult<MemberListItem>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = items,
            }.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving members with filter: {Filter}", filter.Serialize());
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<MemberListItem>>("Failed to retrieve members");
        }
    }

    public async Task<IApiResponse<MemberDetailItem>> GetMemberByIdAsync(string memberId, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetMemberById request for memberId: {MemberId} (admin: {AdminId})", memberId, admin.Id);
            var member = await memberRepo.GetByIdAsync(memberId);
            if (member is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<MemberDetailItem>("Member not found");

            if (!(await IsMemberInScopeAsync(admin, member)))
                return ApiResponseExtensions.ToNotFoundApiResponse<MemberDetailItem>("Member not found");

            var detail = new MemberDetailItem(
                member.Id, member.FirstName, member.LastName, member.Email, member.Phone,
                member.GraduationYear, member.DepartmentId, member.Program, member.Status,
                member.Company, member.JobTitle, member.Location,
                member.LinkedInUrl, member.Bio, member.ProfilePictureUrl,
                member.StudentId, member.CreatedAt, member.LastLoginAt,
                member.MemberNumber, member.IsEmailVerified, member.RejectionCount, member.BanReason,
                member.IsMembershipActive, member.MembershipExpiry, member.MembershipYearsPaid, member.LastMembershipPaidAt);
            return detail.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<MemberDetailItem>("Failed to retrieve member");
        }
    }

    public async Task<IApiResponse<object>> ApproveMemberAsync(string memberId, AuthData admin)
    {
        try
        {
            logger.LogInformation("ApproveMember request for memberId: {MemberId} by admin {AdminId}", memberId, admin.Id);

            var member = await memberRepo.GetByIdAsync(memberId);
            if (member is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Member not found");

            if (!(await IsMemberInScopeAsync(admin, member)))
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Member not found");

            // Generate unique readable member number: SLUG-YEAR-NNNN
            if (string.IsNullOrEmpty(member.MemberNumber))
            {
                // Find the max existing sequence for this year to prevent race conditions
                var prefix = await GetMemberNumberPrefixAsync(member.GraduationYear);
                var existing = await memberRepo.GetAllAsync(m => m.MemberNumber != null && m.MemberNumber.StartsWith(prefix));
                var maxSeq = existing
                    .Select(m => int.TryParse(m.MemberNumber![(prefix.Length)..], out var n) ? n : 0)
                    .DefaultIfEmpty(0)
                    .Max();
                member.MemberNumber = $"{prefix}{(maxSeq + 1):D4}";
            }

            member.Status = "Active";
            member.UpdatedAt = DateTime.UtcNow;
            member.UpdatedBy = admin.Id;
            await memberRepo.UpdateAsync(member);

            await temporalProvider.EnqueueNotificationAsync(
                NotificationRequest.MemberStatusChanged(currentTenant.InstitutionId, member.Id, member.FirstName, "Active", null), logger);
            await SendMemberStatusEmailAsync(member, "Welcome — You're Approved!",
                $"Welcome, {member.FirstName} — your membership has been approved. You now have full access to the portal.",
                "Go to your portal");
            await auditLog.LogAsync(admin, "Member Approved", $"{member.FirstName} {member.LastName} ({member.Email})");

            logger.LogInformation("Member {MemberId} approved with number {MemberNumber} by admin {AdminId}", memberId, member.MemberNumber, admin.Id);
            return new object().ToOkApiResponse("Member approved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error approving member {MemberId} by admin {AdminId}", memberId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to approve member");
        }
    }

    public async Task<IApiResponse<object>> RejectMemberAsync(string memberId, string? reason, AuthData admin)
    {
        try
        {
            logger.LogInformation("RejectMember request for memberId: {MemberId} by admin {AdminId}", memberId, admin.Id);

            var member = await memberRepo.GetByIdAsync(memberId);
            if (member is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Member not found");

            if (!(await IsMemberInScopeAsync(admin, member)))
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Member not found");

            member.RejectionCount += 1;
            member.Status = member.RejectionCount >= MaxRejections ? "Blocked" : "Suspended";
            member.UpdatedAt = DateTime.UtcNow;
            member.UpdatedBy = admin.Id;
            await memberRepo.UpdateAsync(member);

            await temporalProvider.EnqueueNotificationAsync(
                NotificationRequest.MemberStatusChanged(currentTenant.InstitutionId, member.Id, member.FirstName, member.Status, reason), logger);
            await SendMemberStatusEmailAsync(member,
                member.Status == "Blocked" ? "Registration Blocked" : "Registration Not Approved",
                member.Status == "Blocked"
                    ? "Your registration was not approved after multiple attempts, and this email address can no longer be used to register."
                    : string.IsNullOrWhiteSpace(reason)
                        ? "Your registration was not approved this time. You're welcome to register again."
                        : $"Your registration was not approved. Reason: {reason}. You're welcome to register again.",
                "Learn more");
            await auditLog.LogAsync(admin, member.Status == "Blocked" ? "Member Rejected & Blocked" : "Member Rejected",
                $"{member.FirstName} {member.LastName} ({member.Email}){(string.IsNullOrWhiteSpace(reason) ? "" : $" — {reason}")}");

            var msg = member.Status == "Blocked"
                ? "Member rejected and permanently blocked after reaching maximum rejections"
                : $"Member rejected ({member.RejectionCount}/{MaxRejections} rejections)";
            logger.LogInformation("{Msg} for {MemberId} by admin {AdminId}", msg, memberId, admin.Id);
            return new object().ToOkApiResponse(msg);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error rejecting member {MemberId} by admin {AdminId}", memberId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to reject member");
        }
    }

    public async Task<IApiResponse<object>> BanMemberAsync(string memberId, string? reason, AuthData admin)
    {
        try
        {
            logger.LogInformation("BanMember request for memberId: {MemberId} by admin {AdminId}", memberId, admin.Id);

            var member = await memberRepo.GetByIdAsync(memberId);
            if (member is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Member not found");

            if (!(await IsMemberInScopeAsync(admin, member)))
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Member not found");

            member.Status = "Banned";
            member.BanReason = reason;
            member.UpdatedAt = DateTime.UtcNow;
            member.UpdatedBy = admin.Id;
            await memberRepo.UpdateAsync(member);

            await temporalProvider.EnqueueNotificationAsync(
                NotificationRequest.MemberStatusChanged(currentTenant.InstitutionId, member.Id, member.FirstName, "Banned", reason), logger);
            await SendMemberStatusEmailAsync(member, "Account Suspended",
                string.IsNullOrWhiteSpace(reason)
                    ? "Your account has been suspended. Please contact your institution for details."
                    : $"Your account has been suspended. Reason: {reason}.",
                "Learn more");
            await auditLog.LogAsync(admin, "Member Banned",
                $"{member.FirstName} {member.LastName} ({member.Email}){(string.IsNullOrWhiteSpace(reason) ? "" : $" — {reason}")}");

            logger.LogInformation("Member {MemberId} banned by admin {AdminId}", memberId, admin.Id);
            return new object().ToOkApiResponse("Member banned");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error banning member {MemberId} by admin {AdminId}", memberId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to ban member");
        }
    }

    public async Task<IApiResponse<object>> UnbanMemberAsync(string memberId, AuthData admin)
    {
        try
        {
            logger.LogInformation("UnbanMember request for memberId: {MemberId} by admin {AdminId}", memberId, admin.Id);

            var member = await memberRepo.GetByIdAsync(memberId);
            if (member is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Member not found");

            if (!(await IsMemberInScopeAsync(admin, member)))
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Member not found");

            member.Status = "Active";
            member.BanReason = null;
            member.UpdatedAt = DateTime.UtcNow;
            member.UpdatedBy = admin.Id;
            await memberRepo.UpdateAsync(member);

            await temporalProvider.EnqueueNotificationAsync(
                NotificationRequest.MemberStatusChanged(currentTenant.InstitutionId, member.Id, member.FirstName, "Reinstated", null), logger);
            await SendMemberStatusEmailAsync(member, "Account Reinstated",
                $"Good news, {member.FirstName} — your account has been reinstated. You can log back in now.",
                "Go to your portal");
            await auditLog.LogAsync(admin, "Member Unbanned", $"{member.FirstName} {member.LastName} ({member.Email})");

            logger.LogInformation("Member {MemberId} unbanned by admin {AdminId}", memberId, admin.Id);
            return new object().ToOkApiResponse("Member unbanned");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error unbanning member {MemberId} by admin {AdminId}", memberId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to unban member");
        }
    }

    public async Task<IApiResponse<ImportMembersResult>> ImportMembersAsync(ImportMembersRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("ImportMembers request with {Count} items by admin {AdminId}", request.Members.Count, admin.Id);

            var imported = 0;
            var skipped = 0;
            var errors = new List<string>();
            var institution = string.IsNullOrEmpty(currentTenant.InstitutionId)
                ? null
                : await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
            var isCommunityOrg = institution?.OrganizationType == OrganizationTypes.Community;

            foreach (var item in request.Members)
            {
                try
                {
                    // Not-yet-created — no Id to check community membership against. For
                    // Alumni institutions this is a batch/year-group check only (a
                    // ScopedAdmin scoped purely by community, with no batch, can't
                    // bulk-import new members at all). For Community institutions there's
                    // no year group to check at all, so a ScopedAdmin scoped by community
                    // is authorized instead — consistent with IsMemberInScopeAsync's own
                    // dual pathway.
                    var authorized = isCommunityOrg
                        ? admin.CanModifyScopedItem(itemYearGroups: null, createdBy: null,
                            itemCommunityId: admin.CommunityIds is { Count: > 0 } ? admin.CommunityIds[0] : null)
                        : admin.CanModifyScopedItem(new List<int> { item.GraduationYear ?? 0 }, createdBy: null);
                    if (!authorized)
                    {
                        skipped++;
                        errors.Add($"{item.Email}: outside your assigned scope");
                        continue;
                    }

                    var email = item.Email.ToLower().Trim();
                    var existing = await memberRepo.GetOneAsync(m => m.Email == email);
                    if (existing is not null)
                    {
                        skipped++;
                        errors.Add($"{email}: already registered");
                        continue;
                    }

                    // Generate member number
                    var prefix = await GetMemberNumberPrefixAsync(item.GraduationYear);
                    var existingMembers = await memberRepo.GetAllAsync(m => m.MemberNumber != null && m.MemberNumber.StartsWith(prefix));
                    var maxSeq = existingMembers
                        .Select(m => int.TryParse(m.MemberNumber![(prefix.Length)..], out var n) ? n : 0)
                        .DefaultIfEmpty(0)
                        .Max();

                    var member = new Member
                    {
                        FirstName = item.FirstName.Trim(),
                        LastName = item.LastName.Trim(),
                        Email = email,
                        // No usable password yet — see SendMemberWelcomeEmailAsync for the reset-token pattern this pairs with.
                        Password = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                        Phone = item.Phone,
                        StudentId = item.StudentId,
                        GraduationYear = item.GraduationYear ?? 0,
                        DepartmentId = item.DepartmentId ?? string.Empty,
                        Status = "Active",
                        IsEmailVerified = true,
                        EmailVerificationToken = GenerateUrlToken("reset"),
                        EmailVerificationSentAt = DateTime.UtcNow,
                        MemberNumber = $"{prefix}{(maxSeq + 1):D4}",
                        CreatedBy = admin.Id,
                    };
                    await memberRepo.AddAsync(member);

                    if (institution is not null)
                        await SendMemberWelcomeEmailAsync(member, institution);

                    // Record contributions for paid membership years
                    if (item.PaidMembershipYears is { Count: > 0 })
                    {
                        foreach (var year in item.PaidMembershipYears)
                        {
                            var campaign = await campaignRepo.GetOneAsync(c =>
                                c.IsMembershipCampaign && c.MembershipYear == year);
                            if (campaign is null) continue;

                            var contribution = new Contribution
                            {
                                CampaignId = campaign.Id,
                                Campaign = new CampaignSnapshot { Id = campaign.Id, Title = campaign.Title },
                                MemberId = member.Id,
                                Member = new MemberSnapshot
                                {
                                    Id = member.Id, FirstName = member.FirstName,
                                    LastName = member.LastName, Email = member.Email,
                                    MemberNumber = member.MemberNumber,
                                },
                                Amount = campaign.AmountPerMember,
                                PaymentMethod = "Pre-portal",
                                Status = "Successful",
                                ConfirmedAt = DateTime.UtcNow,
                                ConfirmedBy = admin.Id,
                                Notes = "Imported — paid before portal launch",
                                CreatedBy = admin.Id,
                            };
                            await contributionRepo.AddAsync(contribution);
                        }

                        member.IsMembershipActive = true;
                        member.LastMembershipPaidAt = DateTime.UtcNow;
                        member.MembershipYearsPaid = item.PaidMembershipYears.Count;
                        await memberRepo.UpdateAsync(member);
                    }

                    imported++;
                }
                catch (Exception ex)
                {
                    skipped++;
                    errors.Add($"{item.Email}: {ex.Message}");
                }
            }

            logger.LogInformation("ImportMembers completed: {Imported} imported, {Skipped} skipped by admin {AdminId}", imported, skipped, admin.Id);
            return new ImportMembersResult(imported, skipped, errors).ToOkApiResponse($"{imported} members imported, {skipped} skipped");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error importing members by admin {AdminId}", admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<ImportMembersResult>("Failed to import members");
        }
    }

    public async Task<IApiResponse<object>> ActivateMembershipAsync(string memberId, ActivateMembershipRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("ActivateMembership for memberId: {MemberId} years: [{Years}] by admin {AdminId}",
                memberId, string.Join(",", request.MembershipYears), admin.Id);

            var member = await memberRepo.GetByIdAsync(memberId);
            if (member is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Member not found");

            if (!(await IsMemberInScopeAsync(admin, member)))
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Member not found");

            var activatedCount = 0;
            foreach (var year in request.MembershipYears)
            {
                var campaign = await campaignRepo.GetOneAsync(c =>
                    c.IsMembershipCampaign && c.MembershipYear == year);
                if (campaign is null) continue;

                // Check if already paid
                var existingContribution = await contributionRepo.GetOneAsync(c =>
                    c.CampaignId == campaign.Id && c.MemberId == memberId && c.Status == "Successful");
                if (existingContribution is not null) continue;

                var contribution = new Contribution
                {
                    CampaignId = campaign.Id,
                    Campaign = new CampaignSnapshot { Id = campaign.Id, Title = campaign.Title },
                    MemberId = member.Id,
                    Member = new MemberSnapshot
                    {
                        Id = member.Id, FirstName = member.FirstName,
                        LastName = member.LastName, Email = member.Email,
                        ProfilePictureUrl = member.ProfilePictureUrl,
                        MemberNumber = member.MemberNumber,
                    },
                    Amount = campaign.AmountPerMember,
                    PaymentMethod = "Admin-activated",
                    Status = "Successful",
                    ConfirmedAt = DateTime.UtcNow,
                    ConfirmedBy = admin.Id,
                    Notes = "Membership activated by admin (pre-portal payment)",
                    CreatedBy = admin.Id,
                };
                await contributionRepo.AddAsync(contribution);
                activatedCount++;
            }

            // Re-evaluate membership status
            var currentYear = DateTime.UtcNow.Year;
            var requiredCampaigns = await campaignRepo.GetAllAsync(c =>
                c.IsMembershipCampaign
                && c.MembershipYear.HasValue
                && c.MembershipYear.Value >= member.GraduationYear
                && c.MembershipYear.Value <= currentYear);

            var allPaid = true;
            foreach (var campaign in requiredCampaigns)
            {
                var paid = await contributionRepo.GetOneAsync(c =>
                    c.CampaignId == campaign.Id && c.MemberId == memberId && c.Status == "Successful");
                if (paid is null) { allPaid = false; break; }
            }

            member.IsMembershipActive = allPaid;
            member.LastMembershipPaidAt = DateTime.UtcNow;
            member.UpdatedAt = DateTime.UtcNow;
            member.UpdatedBy = admin.Id;
            await memberRepo.UpdateAsync(member);

            logger.LogInformation("Membership activated for {MemberId}: {Count} years by admin {AdminId}", memberId, activatedCount, admin.Id);
            return new object().ToOkApiResponse($"Membership activated for {activatedCount} year(s)");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error activating membership for {MemberId} by admin {AdminId}", memberId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to activate membership");
        }
    }
}
