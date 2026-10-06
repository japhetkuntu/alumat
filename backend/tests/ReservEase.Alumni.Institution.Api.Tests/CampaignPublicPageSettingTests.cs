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

/// <summary>Administrators publish, unpublish and tune a fundraiser's public page.</summary>
public class CampaignPublicPageSettingTests
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

    private static Campaign Existing(bool membership = false, string? communityId = null) => new()
    {
        Id = "c1", Title = "Library", Deadline = DateTime.UtcNow.AddDays(30), Status = CampaignStatus.Active, TargetAmount = 1000, AmountPerMember = 10,
        IsMembershipCampaign = membership, MembershipYear = membership ? DateTime.UtcNow.Year : null, CommunityId = communityId,
    };

    private static UpdateCampaignPublicPageRequest Request(bool published = true, string policy = "OptedIn") => new() { IsPublished = published, NamePolicy = policy };

    [Fact]
    public async Task A_fundraiser_is_not_public_until_an_admin_publishes_it()
    {
        var rig = new Rig();
        await rig.Seed(Existing());

        var dto = (await rig.Service.GetCampaignByIdAsync("c1", Admin)).Data!;

        Assert.False(dto.PublicPage.IsPublished);
        Assert.Equal("OptedIn", dto.PublicPage.NamePolicy);
    }

    [Fact]
    public async Task Publishing_saves_the_choices_and_stamps_the_date()
    {
        var rig = new Rig();
        await rig.Seed(Existing());

        var response = await rig.Service.UpdatePublicPageAsync("c1", new UpdateCampaignPublicPageRequest
        {
            IsPublished = true, NamePolicy = "Everyone", ShowTotalRaised = false, ShowTarget = true, ShowProgress = false,
            ShowContributorCount = true, ShowDeadline = false, Message = "  Thank you all.  ",
        }, Admin);

        Assert.Equal(200, response.Code);
        var saved = (await rig.Db().Campaigns.SingleAsync()).PublicPage!;
        Assert.True(saved.IsPublished);
        Assert.NotNull(saved.PublishedAt);
        Assert.Equal("Everyone", saved.NamePolicy);
        Assert.False(saved.ShowTotalRaised);
        Assert.False(saved.ShowProgress);
        Assert.False(saved.ShowDeadline);
        Assert.Equal("Thank you all.", saved.Message);
    }

    [Fact]
    public async Task Unpublishing_takes_the_page_down_but_keeps_the_settings()
    {
        var rig = new Rig();
        await rig.Seed(Existing());
        await rig.Service.UpdatePublicPageAsync("c1", Request(true, "Everyone"), Admin);

        var response = await rig.Fresh().UpdatePublicPageAsync("c1", Request(false, "Everyone"), Admin);

        Assert.Equal(200, response.Code);
        var saved = (await rig.Db().Campaigns.SingleAsync()).PublicPage!;
        Assert.False(saved.IsPublished);
        Assert.Equal("Everyone", saved.NamePolicy);
    }

    [Fact]
    public async Task Membership_dues_and_community_fundraisers_cannot_be_published()
    {
        var rig = new Rig();
        await rig.Seed(Existing(membership: true));
        Assert.Equal(400, (await rig.Service.UpdatePublicPageAsync("c1", Request(), Admin)).Code);

        var rig2 = new Rig();
        await rig2.Seed(Existing(communityId: "comm-1"));
        Assert.Equal(400, (await rig2.Service.UpdatePublicPageAsync("c1", Request(), Admin)).Code);
    }

    [Fact]
    public async Task An_unknown_name_policy_is_rejected()
    {
        var rig = new Rig();
        await rig.Seed(Existing());

        Assert.Equal(400, (await rig.Service.UpdatePublicPageAsync("c1", Request(policy: "Anonymous"), Admin)).Code);
        Assert.Null((await rig.Db().Campaigns.SingleAsync()).PublicPage);
    }

    [Fact]
    public async Task A_note_over_400_characters_is_rejected()
    {
        var rig = new Rig();
        await rig.Seed(Existing());
        var request = Request();
        request.Message = new string('x', 401);

        Assert.Equal(400, (await rig.Service.UpdatePublicPageAsync("c1", request, Admin)).Code);
    }

    [Fact]
    public async Task Unknown_campaign_is_not_found()
    {
        var rig = new Rig();

        Assert.Equal(404, (await rig.Service.UpdatePublicPageAsync("nope", Request(), Admin)).Code);
    }
}
