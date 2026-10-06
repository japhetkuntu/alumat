using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>Pledging is switched on per fundraiser by its administrators and is off by default.</summary>
public class CampaignPledgeSettingTests
{
    private const string Tenant = "inst-1";
    private static readonly AuthData Admin = new() { Id = "admin-1", FirstName = "Kojo", LastName = "Staff", Role = "SuperAdmin" };

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public CampaignService Service { get; private set; } = null!;
        public Rig() => Fresh();

        public CampaignService Fresh()
        {
            var db = TestDb.Create(DbName, Tenant);
            return Service = new CampaignService(
                new AlumniPgRepository<Campaign>(db), new AlumniPgRepository<Contribution>(db), new AlumniPgRepository<MemberEntity>(db),
                new AlumniPgRepository<CampaignUpdate>(db), Mock.Of<IStorageService>(), Mock.Of<ITemporalClientProvider>(t => t.IsAvailable == false),
                TestDb.Tenant(Tenant, "umat"), Mock.Of<IInstitutionAuditLogService>(), NullLogger<CampaignService>.Instance);
        }

        public AlumniDbContext Db() => TestDb.Create(DbName, Tenant);

        public async Task Seed(Campaign campaign)
        {
            using var db = Db();
            campaign.InstitutionId = Tenant;
            db.Campaigns.Add(campaign);
            await db.SaveChangesAsync();
            Fresh();
        }
    }

    private static CreateCampaignRequest Create(bool pledges, bool membership = false) => new()
    {
        Title = "Library", TargetAmount = 1000, AmountPerMember = 10, Deadline = DateTime.UtcNow.AddDays(30),
        AllowPledges = pledges, IsMembershipCampaign = membership, MembershipYear = membership ? DateTime.UtcNow.Year : null,
    };

    private static UpdateCampaignRequest Update(string id, bool? pledges) => new()
    {
        CampaignId = id, Title = "Library", Deadline = DateTime.UtcNow.AddDays(30), Status = "Active", AllowPledges = pledges,
    };

    private static Campaign Existing(bool pledges, bool membership = false) => new()
    {
        Id = "c1", Title = "Library", Deadline = DateTime.UtcNow.AddDays(30), Status = CampaignStatus.Active, AllowPledges = pledges,
        IsMembershipCampaign = membership, MembershipYear = membership ? DateTime.UtcNow.Year : null, TargetAmount = 1000, AmountPerMember = 10,
    };

    [Fact]
    public void A_new_fundraiser_does_not_take_pledges_unless_asked()
    {
        Assert.False(new Campaign().AllowPledges);
        Assert.False(new CreateCampaignRequest().AllowPledges);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Creating_a_fundraiser_saves_the_pledge_choice_and_returns_it(bool pledges)
    {
        var rig = new Rig();

        var response = await rig.Service.CreateCampaignAsync(Create(pledges), Admin);

        Assert.Equal(201, response.Code);
        Assert.Equal(pledges, response.Data!.AllowPledges);
        Assert.Equal(pledges, (await rig.Db().Campaigns.SingleAsync()).AllowPledges);
    }

    [Fact]
    public async Task Membership_dues_never_take_pledges_even_if_the_request_asks_for_it()
    {
        var rig = new Rig();
        var response = await rig.Service.CreateCampaignAsync(Create(pledges: true, membership: true), Admin);
        Assert.Equal(201, response.Code);
        Assert.False((await rig.Db().Campaigns.SingleAsync()).AllowPledges);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    public async Task Editing_a_fundraiser_can_turn_pledging_on_or_off(bool before, bool requested, bool expected)
    {
        var rig = new Rig();
        await rig.Seed(Existing(before));

        var response = await rig.Service.UpdateCampaignAsync(Update("c1", requested), Admin);

        Assert.Equal(200, response.Code);
        Assert.Equal(expected, (await rig.Db().Campaigns.SingleAsync()).AllowPledges);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_edit_that_says_nothing_about_pledges_leaves_the_setting_as_it_was(bool before)
    {
        var rig = new Rig();
        await rig.Seed(Existing(before));

        await rig.Service.UpdateCampaignAsync(Update("c1", pledges: null), Admin);

        Assert.Equal(before, (await rig.Db().Campaigns.SingleAsync()).AllowPledges);
    }

    [Fact]
    public async Task Turning_pledging_off_does_not_remove_pledges_members_already_made()
    {
        var rig = new Rig();
        await rig.Seed(Existing(true));
        using (var db = rig.Db())
        {
            db.Pledges.Add(new Pledge { Id = "p1", CampaignId = "c1", MemberId = "m1", InstitutionId = Tenant, Amount = 50, DueDate = DateTime.UtcNow.AddDays(5) });
            await db.SaveChangesAsync();
        }
        rig.Fresh();

        await rig.Service.UpdateCampaignAsync(Update("c1", false), Admin);

        Assert.False((await rig.Db().Campaigns.SingleAsync()).AllowPledges);
        Assert.Equal(PledgeStatuses.Open, (await rig.Db().Pledges.SingleAsync()).Status);
    }
}
