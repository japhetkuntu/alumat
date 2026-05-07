using Umat.Alumni.Admin.Api.Extensions;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Repositories;

namespace Umat.Alumni.Admin.Api.Services.Implementations;

public class MemberManagementService(
    IAlumniPgRepository<Member> memberRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    ILogger<MemberManagementService> logger) : IMemberManagementService
{
    private const int MaxRejections = 3;

    public async Task<IApiResponse<PgPagedResult<MemberListItem>>> GetMembersAsync(MemberListFilter filter, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetMembers request with filter: {Filter} (admin: {AdminId})", filter.Serialize(), admin.Id);

            var isSuper = admin.Role == "SuperAdmin";
            var yearGroup = admin.GraduationYear;

            var result = await memberRepo.GetPagedAsync(
                filter.Page, filter.PageSize,
                sortColumn: filter.SortColumn ?? "CreatedAt", sortDir: filter.SortDir ?? "desc",
                f => (isSuper || (yearGroup.HasValue && f.GraduationYear == yearGroup.Value))
                  && (string.IsNullOrEmpty(filter.Status) || f.Status == filter.Status)
                  && (string.IsNullOrEmpty(filter.DepartmentId) || f.DepartmentId == filter.DepartmentId)
                  && (string.IsNullOrEmpty(filter.Search) ||
                      f.FirstName.Contains(filter.Search) ||
                      f.LastName.Contains(filter.Search) ||
                      f.Email.Contains(filter.Search)));

            var items = result.Results.Select(m => new MemberListItem(
                m.Id, m.FirstName, m.LastName, m.Email, m.Phone,
                m.GraduationYear, m.DepartmentId, m.Status, m.Company, m.CreatedAt,
                m.MemberNumber, m.IsEmailVerified, m.RejectionCount, m.ProfilePictureUrl,
                m.IsMembershipActive, m.MembershipExpiry));

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

            var isSuper = admin.Role == "SuperAdmin";
            if (!isSuper)
            {
                if (!admin.GraduationYear.HasValue || member.GraduationYear != admin.GraduationYear.Value)
                    return ApiResponseExtensions.ToNotFoundApiResponse<MemberDetailItem>("Member not found");
            }

            var detail = new MemberDetailItem(
                member.Id, member.FirstName, member.LastName, member.Email, member.Phone,
                member.GraduationYear, member.DepartmentId, member.Status,
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

            // Generate unique readable member number: UMaT/YEAR/NNNN
            if (string.IsNullOrEmpty(member.MemberNumber))
            {
                // Find the max existing sequence for this year to prevent race conditions
                var prefix = $"UMaT-{member.GraduationYear}-";
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

            member.RejectionCount += 1;
            member.Status = member.RejectionCount >= MaxRejections ? "Blocked" : "Suspended";
            member.UpdatedAt = DateTime.UtcNow;
            member.UpdatedBy = admin.Id;
            await memberRepo.UpdateAsync(member);

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

            member.Status = "Banned";
            member.BanReason = reason;
            member.UpdatedAt = DateTime.UtcNow;
            member.UpdatedBy = admin.Id;
            await memberRepo.UpdateAsync(member);

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

            member.Status = "Active";
            member.BanReason = null;
            member.UpdatedAt = DateTime.UtcNow;
            member.UpdatedBy = admin.Id;
            await memberRepo.UpdateAsync(member);

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

            foreach (var item in request.Members)
            {
                try
                {
                    var email = item.Email.ToLower().Trim();
                    var existing = await memberRepo.GetOneAsync(m => m.Email == email);
                    if (existing is not null)
                    {
                        skipped++;
                        errors.Add($"{email}: already registered");
                        continue;
                    }

                    // Generate a temporary password (member must reset on first login)
                    var tempPassword = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

                    // Generate member number
                    var prefix = $"UMaT-{item.GraduationYear}-";
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
                        Password = tempPassword,
                        Phone = item.Phone,
                        StudentId = item.StudentId,
                        GraduationYear = item.GraduationYear,
                        DepartmentId = item.DepartmentId ?? string.Empty,
                        Status = "Active",
                        IsEmailVerified = true,
                        MemberNumber = $"{prefix}{(maxSeq + 1):D4}",
                        CreatedBy = admin.Id,
                    };
                    await memberRepo.AddAsync(member);

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
                                Status = "Confirmed",
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

            var activatedCount = 0;
            foreach (var year in request.MembershipYears)
            {
                var campaign = await campaignRepo.GetOneAsync(c =>
                    c.IsMembershipCampaign && c.MembershipYear == year);
                if (campaign is null) continue;

                // Check if already paid
                var existingContribution = await contributionRepo.GetOneAsync(c =>
                    c.CampaignId == campaign.Id && c.MemberId == memberId && c.Status == "Confirmed");
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
                    Status = "Confirmed",
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
                    c.CampaignId == campaign.Id && c.MemberId == memberId && c.Status == "Confirmed");
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
