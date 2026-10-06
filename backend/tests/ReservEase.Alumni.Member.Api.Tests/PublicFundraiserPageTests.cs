using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

/// <summary>The anonymous page for a published fundraiser: names and admin-chosen totals only, never an individual's amount.</summary>
public class PublicFundraiserPageTests
{
    private const string Tenant = "inst-1";

    private static async Task<CampaignService> Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant))
        {
            foreach (var e in seed) { if (e is PostgresDb.Sdk.Entities.ITenantScoped t) t.InstitutionId = Tenant; db.Add(e); }
            await db.SaveChangesAsync();
        }
        var ctx = TestDb.Create(name, Tenant);
        return new CampaignService(new AlumniPgRepository<Campaign>(ctx), new AlumniPgRepository<MemberEntity>(ctx),
            new AlumniPgRepository<CommunityMembership>(ctx), new AlumniPgRepository<Community>(ctx), new AlumniPgRepository<CampaignUpdate>(ctx),
            new AlumniPgRepository<Contribution>(ctx), NullLogger<CampaignService>.Instance);
    }

    private static Campaign Fundraiser(Action<CampaignPublicPage>? tune = null, Action<Campaign>? edit = null)
    {
        var page = new CampaignPublicPage { IsPublished = true };
        tune?.Invoke(page);
        var c = new Campaign { Id = "c1", Title = "Library", Description = "Books", TargetAmount = 1000, AmountPerMember = 10, Status = CampaignStatus.Active, Deadline = DateTime.UtcNow.AddDays(10), PublicPage = page };
        edit?.Invoke(c);
        return c;
    }

    private static Contribution Gift(string id, string memberId, string first, decimal amount, bool optedIn, int daysAgo = 1, string status = "Successful") => new()
    {
        Id = id, CampaignId = "c1", MemberId = memberId, Amount = amount, Status = status, ShowOnWallOfSupport = optedIn,
        ConfirmedAt = DateTime.UtcNow.AddDays(-daysAgo), Member = new MemberSnapshot { Id = memberId, FirstName = first, LastName = "Doe" },
    };

    [Fact]
    public async Task An_unpublished_fundraiser_is_not_found()
    {
        var service = await Create(Fundraiser(p => p.IsPublished = false));

        Assert.Equal(404, (await service.GetPublicFundraiserAsync("c1")).Code);
    }

    [Fact]
    public async Task A_fundraiser_that_was_never_configured_is_not_found()
    {
        var service = await Create(Fundraiser(edit: c => c.PublicPage = null));

        Assert.Equal(404, (await service.GetPublicFundraiserAsync("c1")).Code);
    }

    [Fact]
    public async Task An_unknown_fundraiser_is_not_found()
        => Assert.Equal(404, (await (await Create()).GetPublicFundraiserAsync("nope")).Code);

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "comm-1")]
    public async Task Membership_dues_and_community_fundraisers_are_never_public_even_if_flagged(bool membership, string? community)
    {
        var service = await Create(Fundraiser(edit: c => { c.IsMembershipCampaign = membership; c.CommunityId = community; }));

        Assert.Equal(404, (await service.GetPublicFundraiserAsync("c1")).Code);
    }

    [Fact]
    public async Task An_archived_fundraiser_is_not_found()
    {
        var service = await Create(Fundraiser(edit: c => c.Status = CampaignStatus.Archived));

        Assert.Equal(404, (await service.GetPublicFundraiserAsync("c1")).Code);
    }

    [Fact]
    public async Task Opted_in_policy_lists_only_givers_who_ticked_the_box()
    {
        var service = await Create(Fundraiser(),
            new MemberEntity { Id = "a", FirstName = "Ama", LastName = "Doe", Email = "a@x.com" },
            new MemberEntity { Id = "b", FirstName = "Kofi", LastName = "Doe", Email = "b@x.com" },
            Gift("g1", "a", "Ama", 50, optedIn: true), Gift("g2", "b", "Kofi", 70, optedIn: false));

        var dto = (await service.GetPublicFundraiserAsync("c1")).Data!;

        Assert.Equal(new[] { "Ama Doe" }, dto.Contributors);
    }

    [Fact]
    public async Task Everyone_policy_lists_every_confirmed_giver_but_never_pending_or_rejected()
    {
        var service = await Create(Fundraiser(p => p.NamePolicy = PublicNamePolicy.Everyone),
            Gift("g1", "a", "Ama", 50, optedIn: true, daysAgo: 3), Gift("g2", "b", "Kofi", 70, optedIn: false, daysAgo: 2),
            Gift("g3", "c", "Esi", 10, optedIn: true, status: "Pending"), Gift("g4", "d", "Yaw", 10, optedIn: true, status: "Rejected"));

        var dto = (await service.GetPublicFundraiserAsync("c1")).Data!;

        Assert.Equal(new[] { "Ama Doe", "Kofi Doe" }, dto.Contributors);
    }

    [Fact]
    public async Task A_person_who_gave_twice_appears_once()
    {
        var service = await Create(Fundraiser(p => p.NamePolicy = PublicNamePolicy.Everyone),
            Gift("g1", "a", "Ama", 50, true, 3), Gift("g2", "a", "Ama", 20, true, 1));

        var dto = (await service.GetPublicFundraiserAsync("c1")).Data!;

        Assert.Single(dto.Contributors);
        Assert.Equal(1, dto.ContributorCount);
        Assert.Equal(70, dto.TotalRaised);
    }

    [Fact]
    public async Task A_name_changed_since_giving_shows_the_current_name()
    {
        var service = await Create(Fundraiser(),
            new MemberEntity { Id = "a", FirstName = "Abena", LastName = "Boateng", Email = "a@x.com" },
            Gift("g1", "a", "Ama", 50, true));

        Assert.Equal(new[] { "Abena Boateng" }, (await service.GetPublicFundraiserAsync("c1")).Data!.Contributors);
    }

    [Fact]
    public async Task Only_the_figures_the_admin_chose_are_returned()
    {
        var service = await Create(Fundraiser(p => { p.ShowTotalRaised = true; p.ShowTarget = false; p.ShowProgress = false; p.ShowContributorCount = false; p.ShowDeadline = false; }),
            Gift("g1", "a", "Ama", 250, true));

        var dto = (await service.GetPublicFundraiserAsync("c1")).Data!;

        Assert.Equal(250, dto.TotalRaised);
        Assert.Null(dto.TargetAmount);
        Assert.Null(dto.ProgressPercent);
        Assert.Null(dto.ContributorCount);
        Assert.Null(dto.Deadline);
    }

    [Fact]
    public async Task Progress_is_a_whole_percent_of_the_target_and_capped_at_100()
    {
        var half = await Create(Fundraiser(), Gift("g1", "a", "Ama", 505, true));
        Assert.Equal(50, (await half.GetPublicFundraiserAsync("c1")).Data!.ProgressPercent);

        var over = await Create(Fundraiser(), Gift("g1", "a", "Ama", 5000, true));
        Assert.Equal(100, (await over.GetPublicFundraiserAsync("c1")).Data!.ProgressPercent);
    }

    [Fact]
    public async Task No_individual_amount_ever_appears_in_the_response()
    {
        var service = await Create(Fundraiser(p => p.NamePolicy = PublicNamePolicy.Everyone, edit: c => c.TargetAmount = 99999),
            Gift("g1", "a", "Ama", 1234.56m, true), Gift("g2", "b", "Kofi", 777.77m, false));

        var json = JsonSerializer.Serialize((await service.GetPublicFundraiserAsync("c1")).Data);

        Assert.DoesNotContain("1234.56", json);
        Assert.DoesNotContain("777.77", json);
        Assert.DoesNotContain("@x.com", json);
    }

    [Fact]
    public async Task A_closed_fundraiser_stays_visible_but_no_longer_open_for_giving()
    {
        var service = await Create(Fundraiser(edit: c => c.Status = CampaignStatus.Closed));
        var dto = (await service.GetPublicFundraiserAsync("c1")).Data!;

        Assert.False(dto.IsOpenForGiving);

        var open = (await (await Create(Fundraiser())).GetPublicFundraiserAsync("c1")).Data!;
        Assert.True(open.IsOpenForGiving);
    }
}
