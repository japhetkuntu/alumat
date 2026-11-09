using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Text;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class ManageFundraisingService(
    GroupGuard guard,
    GroupMedia media,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<InstitutionAuditLogEntry> auditRepo,
    ICurrentTenantService currentTenant,
    ITemporalClientProvider temporalProvider,
    ILogger<ManageFundraisingService> logger) : IManageFundraisingService
{
    private const int MaxListed = 100;
    private const int MaxGifts = 200;
    private static readonly string[] Either = [LeadershipPrivileges.ManageFundraisers, LeadershipPrivileges.ViewGiving];

    public async Task<IApiResponse<List<GroupCampaignDto>>> GetCampaignsAsync(string memberId, string scopeType, string scopeId)
    {
        try
        {
            var (group, refusal, held) = await guard.GuardAnyAsync<List<GroupCampaignDto>>(memberId, Either, scopeType, scopeId);
            if (refusal is not null) return refusal;
            var communityScope = group!.Community?.Id;
            var campaigns = (await campaignRepo.GetAllAsync(c => c.CommunityId == communityScope && !c.IsMembershipCampaign))
                .Where(c => group.IsExactly(c.CommunityId, c.YearGroups));
            return campaigns.OrderByDescending(c => c.CreatedAt).Take(MaxListed).Select(c => ToDto(c, memberId, held)).ToList().ToOkApiResponse();
        }
        catch (Exception e) { return Fail<List<GroupCampaignDto>>(e, "list fundraisers", "Failed to load the fundraisers"); }
    }

    public async Task<IApiResponse<GroupCampaignDto>> CreateCampaignAsync(string memberId, string scopeType, string scopeId, SaveGroupCampaignRequest request)
    {
        try
        {
            var (group, refusal) = await guard.GuardAsync<GroupCampaignDto>(memberId, LeadershipPrivileges.ManageFundraisers, scopeType, scopeId);
            if (refusal is not null) return refusal;
            if (Clean(request, mustBeAhead: true, out var title, out var description) is { } problem) return ApiResponseExtensions.ToBadRequestApiResponse<GroupCampaignDto>(problem);
            if (GroupMedia.CleanVideos(request.YoutubeVideoUrls?.Take(1), out var videos) is { } videoProblem) return ApiResponseExtensions.ToBadRequestApiResponse<GroupCampaignDto>(videoProblem);
            if (await GroupMedia.CheckAsync(request.BannerImage, null) is { } picture) return ApiResponseExtensions.ToBadRequestApiResponse<GroupCampaignDto>(picture);

            var (communityId, years) = group!.Audience;
            // No account details, no manual payments, no pledges, no banner: the leader sets what the fundraiser is, never where money goes.
            var campaign = new Campaign
            {
                CommunityId = communityId, YearGroups = years, Title = title, Description = description,
                TargetAmount = request.TargetAmount, AmountPerMember = request.AmountPerMember,
                Deadline = DateTime.SpecifyKind(request.Deadline, DateTimeKind.Utc), Status = CampaignStatus.Active,
                AllowManualPayments = false, AllowPledges = false, IsMembershipCampaign = false, CreatedBy = memberId,
                YoutubeVideoUrl = videos?.FirstOrDefault(),
                BannerImageUrl = request.BannerImage is null ? null : await media.UploadAsync(request.BannerImage),
            };
            await campaignRepo.AddAsync(campaign);
            await EnqueueAsync(NotificationRequest.CampaignAlert(currentTenant.InstitutionId ?? "", campaign.Id));
            await AuditAsync(memberId, "Fundraiser Started", $"{title}, {group.Name}: target {request.TargetAmount:0.##}");
            return ToDto(campaign, memberId, [LeadershipPrivileges.ManageFundraisers]).ToCreatedApiResponse("Fundraiser started");
        }
        catch (Exception e) { return Fail<GroupCampaignDto>(e, "start fundraiser", "Failed to start the fundraiser"); }
    }

    public async Task<IApiResponse<GroupCampaignDto>> UpdateCampaignAsync(string memberId, string scopeType, string scopeId, string id, SaveGroupCampaignRequest request)
    {
        try
        {
            var (group, refusal) = await guard.GuardAsync<GroupCampaignDto>(memberId, LeadershipPrivileges.ManageFundraisers, scopeType, scopeId);
            if (refusal is not null) return refusal;
            var campaign = await OwnActiveAsync(group!, memberId, id);
            if (campaign is null) return ApiResponseExtensions.ToNotFoundApiResponse<GroupCampaignDto>("That fundraiser was not found");
            if (Clean(request, mustBeAhead: request.Deadline != campaign.Deadline, out var title, out var description) is { } problem)
                return ApiResponseExtensions.ToBadRequestApiResponse<GroupCampaignDto>(problem);
            if (GroupMedia.CleanVideos(request.YoutubeVideoUrls?.Take(1), out var videos) is { } videoProblem) return ApiResponseExtensions.ToBadRequestApiResponse<GroupCampaignDto>(videoProblem);
            if (await GroupMedia.CheckAsync(request.BannerImage, null) is { } picture) return ApiResponseExtensions.ToBadRequestApiResponse<GroupCampaignDto>(picture);
            campaign.YoutubeVideoUrl = videos?.FirstOrDefault();
            campaign.BannerImageUrl = request.BannerImage is not null ? await media.UploadAsync(request.BannerImage) : request.RemoveBanner ? null : campaign.BannerImageUrl;

            // Once anyone has given, what each person was asked for is what they agreed to, so it no longer changes.
            var hasGifts = campaign.PaidCount > 0 || campaign.CollectedAmount > 0 || await contributionRepo.CountAsync(c => c.CampaignId == campaign.Id) > 0;
            if (hasGifts && request.AmountPerMember != campaign.AmountPerMember)
                return ApiResponseExtensions.ToBadRequestApiResponse<GroupCampaignDto>("Someone has already given, so the amount asked of each member can't change");

            campaign.Title = title; campaign.Description = description; campaign.TargetAmount = request.TargetAmount; campaign.AmountPerMember = request.AmountPerMember;
            campaign.Deadline = DateTime.SpecifyKind(request.Deadline, DateTimeKind.Utc); campaign.UpdatedAt = DateTime.UtcNow; campaign.UpdatedBy = memberId;
            await campaignRepo.UpdateAsync(campaign);
            await AuditAsync(memberId, "Fundraiser Edited", $"{title}, {group!.Name}");
            return ToDto(campaign, memberId, [LeadershipPrivileges.ManageFundraisers]).ToOkApiResponse("Saved");
        }
        catch (Exception e) { return Fail<GroupCampaignDto>(e, "edit fundraiser", "Failed to save the fundraiser"); }
    }

    public async Task<IApiResponse<object>> CloseCampaignAsync(string memberId, string scopeType, string scopeId, string id)
    {
        try
        {
            var (group, refusal) = await guard.GuardAsync<object>(memberId, LeadershipPrivileges.ManageFundraisers, scopeType, scopeId);
            if (refusal is not null) return refusal;
            var campaign = await OwnActiveAsync(group!, memberId, id);
            if (campaign is null) return ApiResponseExtensions.ToNotFoundApiResponse<object>("That fundraiser was not found, or it is already closed");
            campaign.Status = CampaignStatus.Closed; campaign.UpdatedAt = DateTime.UtcNow; campaign.UpdatedBy = memberId;
            await campaignRepo.UpdateAsync(campaign);
            await AuditAsync(memberId, "Fundraiser Closed", $"{campaign.Title}, {group!.Name}");
            return new object().ToOkApiResponse("Closed");
        }
        catch (Exception e) { return Fail<object>(e, "close fundraiser", "Failed to close the fundraiser"); }
    }

    public async Task<IApiResponse<List<GroupGiftDto>>> GetGiftsAsync(string memberId, string scopeType, string scopeId, string id)
    {
        try
        {
            var (group, refusal) = await guard.GuardAsync<List<GroupGiftDto>>(memberId, LeadershipPrivileges.ViewGiving, scopeType, scopeId);
            if (refusal is not null) return refusal;
            var campaign = (await campaignRepo.GetAllAsync(c => c.Id == id)).FirstOrDefault(c => !c.IsMembershipCampaign && group!.IsExactly(c.CommunityId, c.YearGroups));
            if (campaign is null) return ApiResponseExtensions.ToNotFoundApiResponse<List<GroupGiftDto>>("That fundraiser was not found");

            var gifts = (await contributionRepo.GetAllAsync(c => c.CampaignId == campaign.Id && c.Status != "Rejected")).OrderByDescending(c => c.CreatedAt).Take(MaxGifts).ToList();
            var ids = gifts.Select(g => g.MemberId).Distinct().ToList();
            var people = (await memberRepo.GetAllAsync(m => ids.Contains(m.Id))).ToDictionary(m => m.Id);
            // Only what a thank-you needs: who, how much, whether it has landed, and how. Never fees, net amounts, references or proof of payment.
            return gifts.Select(g =>
            {
                var who = people.GetValueOrDefault(g.MemberId);
                var name = who is not null ? $"{who.FirstName} {who.LastName}".Trim() : g.Member is not null ? $"{g.Member.FirstName} {g.Member.LastName}".Trim() : "A guest";
                return new GroupGiftDto(string.IsNullOrWhiteSpace(name) ? "A guest" : name, who?.ProfilePictureUrl, g.Amount, g.Status == "Successful" ? "Received" : "Waiting for confirmation", MethodLabel(g.PaymentMethod), g.CreatedAt);
            }).ToList().ToOkApiResponse();
        }
        catch (Exception e) { return Fail<List<GroupGiftDto>>(e, "list gifts", "Failed to load who has given"); }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<Campaign?> OwnActiveAsync(GroupRef group, string memberId, string id)
        => (await campaignRepo.GetAllAsync(c => c.Id == id)).FirstOrDefault(c => c.CreatedBy == memberId && !c.IsMembershipCampaign && c.Status == CampaignStatus.Active && group.IsExactly(c.CommunityId, c.YearGroups));

    private static GroupCampaignDto ToDto(Campaign c, string memberId, IReadOnlyCollection<string> held)
        => new(c.Id, c.Title, c.Description, c.TargetAmount, c.AmountPerMember, c.Deadline, c.Status.ToString(), c.CollectedAmount, c.PaidCount,
            CanEdit: c.CreatedBy == memberId && c.Status == CampaignStatus.Active && held.Contains(LeadershipPrivileges.ManageFundraisers),
            CanSeeGiving: held.Contains(LeadershipPrivileges.ViewGiving), c.BannerImageUrl, c.YoutubeVideoUrl);

    private static string MethodLabel(string method)
        => method.Contains("paystack", StringComparison.OrdinalIgnoreCase) ? "Online"
            : method.Contains("manual", StringComparison.OrdinalIgnoreCase) ? "Bank or mobile money" : "Other";

    private static string? Clean(SaveGroupCampaignRequest r, bool mustBeAhead, out string title, out string? description)
    {
        description = null;
        title = string.Join(" ", (r.Title ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (title.Length == 0) return "Give the fundraiser a title";
        if (title.Length > 200) return "Keep the title to 200 characters";
        if (r.TargetAmount <= 0) return "Set a target that is more than zero";
        if (r.AmountPerMember <= 0) return "Set an amount per member that is more than zero";
        if (r.TargetAmount > 100_000_000 || r.AmountPerMember > 10_000_000) return "That amount is too large";
        if (mustBeAhead && r.Deadline.ToUniversalTime() <= DateTime.UtcNow) return "Choose a closing date that is still ahead";
        description = RichText.Sanitize(r.Description);
        return RichText.IsTooLong(description) ? RichText.TooLongMessage : null;
    }

    private IApiResponse<T> Fail<T>(Exception e, string what, string message)
    {
        logger.LogError(e, "Error trying to {What}", what);
        return ApiResponseExtensions.ToServerErrorApiResponse<T>(message);
    }

    private async Task EnqueueAsync(NotificationRequest request)
    {
        try { await temporalProvider.EnqueueNotificationAsync(request, logger); }
        catch (Exception e) { logger.LogWarning(e, "Could not queue a {Kind} notification", request.Kind); }
    }

    private async Task AuditAsync(string memberId, string action, string target)
    {
        try
        {
            var who = await memberRepo.GetByIdAsync(memberId) is { } m ? $"{m.FirstName} {m.LastName}".Trim() : "A leader";
            await auditRepo.AddAsync(new InstitutionAuditLogEntry { ActorId = memberId, Actor = $"{who} (leader)", Action = action, Target = target, CreatedBy = memberId });
        }
        catch (Exception e) { logger.LogWarning(e, "Could not record {Action} in the audit log", action); }
    }
}
