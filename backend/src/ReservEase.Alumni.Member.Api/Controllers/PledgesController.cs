using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>
/// A member's own pledges. A pledge is a non-binding intention to give to a fundraiser by a date; it moves no money.
/// Members only ever see their own pledges here. Nobody else's, and no pledged totals, are exposed to members.
/// </summary>
[Authorize]
[RequireFeature(InstitutionFeatures.Contributions)]
public class PledgesController(
    IAlumniPgRepository<Pledge> pledgeRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    IAlumniPgRepository<PostgresDb.Sdk.Entities.Alumni.Member> memberRepo,
    ILogger<PledgesController> logger) : DefaultController
{
    private const decimal MaxPledgeAmount = 10_000_000m;
    private const int MaxPledgeYearsAhead = 5;

    [HttpGet]
    [SwaggerOperation(Summary = "My pledges", Description = "The current member's own pledges with how much of each has been paid so far.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<MemberPledgeDto>>))]
    public async Task<IActionResult> GetMine()
    {
        var member = User.GetAccount();
        var pledges = (await pledgeRepo.GetAllAsync(p => p.MemberId == member.Id)).OrderByDescending(p => p.CreatedAt).ToList();
        if (pledges.Count == 0) return new List<MemberPledgeDto>().ToOkApiResponse().ToActionResult();

        var campaignIds = pledges.Select(p => p.CampaignId).Distinct().ToList();
        var campaigns = (await campaignRepo.GetAllAsync(c => campaignIds.Contains(c.Id))).ToDictionary(c => c.Id);
        var contributions = (await contributionRepo.GetAllAsync(c => c.MemberId == member.Id && campaignIds.Contains(c.CampaignId))).ToList();

        var now = DateTime.UtcNow;
        var result = pledges.Select(p =>
        {
            var progress = PledgeProgress.Compute(p, contributions.Where(c => c.CampaignId == p.CampaignId), now);
            campaigns.TryGetValue(p.CampaignId, out var campaign);
            return new MemberPledgeDto(p.Id, p.CampaignId, campaign?.Title ?? "Fundraiser", p.Amount, progress.Paid, progress.Outstanding,
                p.DueDate, progress.State, p.CreatedAt);
        }).ToList();
        return result.ToOkApiResponse().ToActionResult();
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Make a pledge", Description = "Promise to give to a fundraiser by a date. Not binding, and no money moves.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<MemberPledgeDto>))]
    public async Task<IActionResult> Create([FromBody] CreatePledgeRequest request)
    {
        var member = User.GetAccount();

        if (request.Amount <= 0 || request.Amount > MaxPledgeAmount)
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("Enter the amount you plan to give.").ToActionResult();

        var due = DateTime.SpecifyKind(request.DueDate.Date, DateTimeKind.Utc);
        if (due < DateTime.UtcNow.Date)
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("Choose a date that is today or later.").ToActionResult();
        if (due > DateTime.UtcNow.Date.AddYears(MaxPledgeYearsAhead))
            return ApiResponseExtensions.ToBadRequestApiResponse<object>($"Choose a date within the next {MaxPledgeYearsAhead} years.").ToActionResult();

        var campaign = await campaignRepo.GetByIdAsync(request.CampaignId);
        // Same rule as viewing a campaign: a community's private fundraiser looks "not found" to anyone who isn't an approved member.
        if (campaign is null
            || campaign.IsMembershipCampaign
            || (!string.IsNullOrEmpty(campaign.CommunityId)
                && await membershipRepo.GetOneAsync(m => m.CommunityId == campaign.CommunityId && m.MemberId == member.Id && m.Status == "Approved") is null))
            return ApiResponseExtensions.ToNotFoundApiResponse<object>("Fundraiser not found").ToActionResult();
        if (campaign.Status != CampaignStatus.Active)
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("This fundraiser is no longer accepting contributions.").ToActionResult();
        // Pledging is something an administrator turns on for each fundraiser; it is off by default.
        if (!campaign.AllowPledges)
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("This fundraiser isn't taking pledges.").ToActionResult();

        // One open pledge per fundraiser keeps the admin's list and the reminders unambiguous. Cancel it first to change it.
        var existing = await pledgeRepo.GetOneAsync(p => p.MemberId == member.Id && p.CampaignId == campaign.Id && p.Status == PledgeStatuses.Open);
        if (existing is not null)
        {
            var paid = await contributionRepo.GetAllAsync(c => c.MemberId == member.Id && c.CampaignId == campaign.Id);
            if (PledgeProgress.Compute(existing, paid, DateTime.UtcNow).State != PledgeStates.Fulfilled)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("You already have an open pledge to this fundraiser. Cancel it first if you want to change it.").ToActionResult();
        }

        var profile = await memberRepo.GetByIdAsync(member.Id);
        var pledge = new Pledge
        {
            CampaignId = campaign.Id,
            MemberId = member.Id,
            MemberName = profile is null ? member.Name : $"{profile.FirstName} {profile.LastName}".Trim(),
            Amount = decimal.Round(request.Amount, 2),
            DueDate = due,
            CreatedBy = member.Id,
        };
        await pledgeRepo.AddAsync(pledge);
        logger.LogInformation("Pledge {PledgeId} of {Amount} created by member {MemberId} for campaign {CampaignId}", pledge.Id, pledge.Amount, member.Id, campaign.Id);

        return new MemberPledgeDto(pledge.Id, campaign.Id, campaign.Title, pledge.Amount, 0m, pledge.Amount, pledge.DueDate, PledgeStates.Pledged, pledge.CreatedAt)
            .ToOkApiResponse("Thank you. We'll remind you as your date gets close.").ToActionResult();
    }

    [HttpDelete("{id}")]
    [SwaggerOperation(Summary = "Cancel my pledge")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Cancel(string id)
    {
        var member = User.GetAccount();
        var pledge = await pledgeRepo.GetByIdAsync(id);
        if (pledge is null || pledge.MemberId != member.Id)
            return ApiResponseExtensions.ToNotFoundApiResponse<object>("Pledge not found").ToActionResult();

        if (pledge.Status == PledgeStatuses.Open)
        {
            pledge.Status = PledgeStatuses.Cancelled;
            pledge.StatusNote = "Cancelled by the member";
            pledge.UpdatedAt = DateTime.UtcNow;
            pledge.UpdatedBy = member.Id;
            await pledgeRepo.UpdateAsync(pledge);
        }
        return new object().ToOkApiResponse("Pledge cancelled").ToActionResult();
    }
}

public record CreatePledgeRequest(string CampaignId, decimal Amount, DateTime DueDate);

public record MemberPledgeDto(
    string Id, string CampaignId, string CampaignTitle, decimal Amount, decimal Paid, decimal Outstanding,
    DateTime DueDate, string State, DateTime CreatedAt);
