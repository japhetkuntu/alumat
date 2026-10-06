using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>Administrators connect a community's WhatsApp group and see how invitations are bringing people in.</summary>
public class CommunityChannelAdminTests
{
    private const string Tenant = "inst-1";

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public CommunityService Service { get; private set; } = null!;
        public Rig() => Fresh();

        public CommunityService Fresh()
        {
            var db = TestDb.Create(DbName, Tenant);
            return Service = new CommunityService(new AlumniPgRepository<Community>(db), new AlumniPgRepository<CommunityMembership>(db),
                new AlumniPgRepository<MemberEntity>(db), new AlumniPgRepository<Referral>(db), NullLogger<CommunityService>.Instance);
        }

        public AlumniDbContext Db() => TestDb.Create(DbName, Tenant);

        public async Task Seed(params object[] entities)
        {
            using var db = Db();
            foreach (var e in entities) { if (e is ReservEase.Alumni.PostgresDb.Sdk.Entities.ITenantScoped t) t.InstitutionId = Tenant; db.Add(e); }
            await db.SaveChangesAsync();
            Fresh();
        }
    }

    private static UpdateCommunityRequest Update(List<CommunityChannelInputItem>? channels) =>
        new() { Name = "Mining 2018", IsActive = true, ExternalChannels = channels };

    [Fact]
    public async Task An_admin_can_connect_a_group_and_the_list_shows_it()
    {
        var rig = new Rig();
        await rig.Seed(new Community { Id = "c1", Name = "Mining 2018" });

        var saved = await rig.Service.UpdateCommunityAsync("c1", Update([new("WhatsApp", "Chat", "https://chat.whatsapp.com/AbC")]), "admin");

        Assert.Equal(200, saved.Code);
        var listed = (await rig.Fresh().GetCommunitiesAsync()).Data!.Single();
        Assert.Equal("Chat", listed.Channels!.Single().DisplayName);
    }

    [Fact]
    public async Task Leaving_the_channels_out_of_an_update_leaves_them_alone()
    {
        var rig = new Rig();
        await rig.Seed(new Community { Id = "c1", Name = "Mining 2018", ExternalChannels = [new CommunityChannel { DisplayName = "Chat", InviteUrl = "https://chat.whatsapp.com/AbC" }] });

        await rig.Service.UpdateCommunityAsync("c1", Update(null), "admin");

        Assert.Single((await rig.Db().Communities.SingleAsync()).ExternalChannels!);
    }

    [Fact]
    public async Task A_bad_invite_link_rejects_the_whole_update()
    {
        var rig = new Rig();
        await rig.Seed(new Community { Id = "c1", Name = "Old name" });

        var response = await rig.Service.UpdateCommunityAsync("c1", Update([new("WhatsApp", "Chat", "https://evil.example.com/x")]), "admin");

        Assert.Equal(400, response.Code);
        Assert.Equal("Old name", (await rig.Db().Communities.SingleAsync()).Name);
    }

    [Fact]
    public async Task Growth_counts_invited_joins_per_community_and_per_channel()
    {
        var rig = new Rig();
        await rig.Seed(new Community { Id = "c1", Name = "Mining 2018" }, new Community { Id = "c2", Name = "Civil 2015" },
            new Referral { ReferrerId = "a", ReferredEmail = "1@x.com", CommunityId = "c1", Channel = "whatsapp" },
            new Referral { ReferrerId = "a", ReferredEmail = "2@x.com", CommunityId = "c1", Channel = "whatsapp" },
            new Referral { ReferrerId = "b", ReferredEmail = "3@x.com", CommunityId = "c2" },
            new Referral { ReferrerId = "b", ReferredEmail = "4@x.com", CommunityId = "c2", Channel = "qr", CreatedAt = DateTime.UtcNow.AddDays(-90) },
            new Referral { ReferrerId = "b", ReferredEmail = "5@x.com" });

        var growth = (await rig.Service.GetGrowthAsync()).Data!;

        Assert.Equal((4, 3), (growth.TotalViaInvites, growth.Last30Days));
        Assert.Equal(("whatsapp", 2), (growth.BySource[0].Source, growth.BySource[0].Count));
        Assert.Contains(growth.BySource, s => s is { Source: "link", Count: 1 });
        Assert.Equal(("Mining 2018", 2, 2), (growth.ByCommunity[0].Name, growth.ByCommunity[0].Joined, growth.ByCommunity[0].Last30Days));
    }

    [Fact]
    public async Task With_no_invitations_growth_is_simply_empty()
    {
        var growth = (await new Rig().Service.GetGrowthAsync()).Data!;

        Assert.Equal(0, growth.TotalViaInvites);
        Assert.Empty(growth.BySource);
        Assert.Empty(growth.ByCommunity);
    }

    [Fact]
    public async Task The_list_reports_invited_joins_for_each_community()
    {
        var rig = new Rig();
        await rig.Seed(new Community { Id = "c1", Name = "Mining 2018" },
            new Referral { ReferrerId = "a", ReferredEmail = "1@x.com", CommunityId = "c1" },
            new Referral { ReferrerId = "a", ReferredEmail = "2@x.com", CommunityId = "c1", CreatedAt = DateTime.UtcNow.AddDays(-45) });

        var item = (await rig.Service.GetCommunitiesAsync()).Data!.Single();

        Assert.Equal((2, 1), (item.JoinedViaInvites, item.JoinedLast30Days));
    }
}
