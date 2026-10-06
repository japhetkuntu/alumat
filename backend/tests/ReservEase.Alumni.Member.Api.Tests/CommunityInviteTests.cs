using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

/// <summary>A community leader's invitation details and the WhatsApp group link they can connect.</summary>
public class CommunityInviteTests
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

    private static MemberEntity Person(string id, string? code = null) => new() { Id = id, FirstName = "Ama", LastName = "Mensah", Email = $"{id}@x.com", ReferralCode = code };
    private static CommunityMembership Link(string member, string role = "Member", string status = "Approved") => new() { CommunityId = "c1", MemberId = member, Role = role, Status = status };
    private static Community Group() => new() { Id = "c1", Name = "Mining 2018" };

    [Fact]
    public async Task A_leader_gets_their_own_code_and_the_growth_so_far()
    {
        var rig = new Rig();
        await rig.Seed(Person("lead", "AMA-111"), Group(), Link("lead", "Leader"), Link("m1"), Link("m2", status: "Pending"),
            new Referral { ReferrerId = "lead", ReferredEmail = "a@x.com", CommunityId = "c1", Status = "Registered" },
            new Referral { ReferrerId = "lead", ReferredEmail = "b@x.com", CommunityId = "c1", Status = "Registered", CreatedAt = DateTime.UtcNow.AddDays(-60) },
            new Referral { ReferrerId = "lead", ReferredEmail = "c@x.com", Status = "Registered" });

        var info = (await rig.Service.GetInviteInfoAsync("c1", "lead")).Data!;

        Assert.Equal(("AMA-111", "Mining 2018"), (info.ReferralCode, info.CommunityName));
        Assert.Equal((2, 1, 2, 1), (info.ApprovedMembers, info.PendingRequests, info.JoinedViaInvites, info.JoinedLast30Days));
        Assert.Empty(info.Channels);
    }

    [Fact]
    public async Task A_leader_without_a_code_yet_gets_one_that_stays_the_same()
    {
        var rig = new Rig();
        await rig.Seed(Person("lead"), Group(), Link("lead", "Leader"));

        var first = (await rig.Service.GetInviteInfoAsync("c1", "lead")).Data!.ReferralCode;
        var second = (await rig.Fresh().GetInviteInfoAsync("c1", "lead")).Data!.ReferralCode;

        Assert.StartsWith("AMAMEN-", first);
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("Member", "Approved")]
    [InlineData("Leader", "Pending")]
    [InlineData("none", "none")]
    public async Task Only_an_approved_leader_can_see_invitation_details_or_connect_a_group(string role, string status)
    {
        var rig = new Rig();
        await rig.Seed(Person("x", "X-1"), Group(), role == "none" ? new Community { Id = "other", Name = "Other" } : Link("x", role, status));

        Assert.Equal(404, (await rig.Service.GetInviteInfoAsync("c1", "x")).Code);
        var save = await rig.Fresh().UpdateChannelsAsync("c1", new UpdateCommunityChannelsRequest([new("WhatsApp", "Group", null)]), "x");
        Assert.Equal(404, save.Code);
        Assert.Null((await rig.Db().Communities.SingleAsync(c => c.Id == "c1")).ExternalChannels);
    }

    [Fact]
    public async Task A_leader_can_connect_a_whatsapp_group_and_approved_members_then_see_it()
    {
        var rig = new Rig();
        await rig.Seed(Person("lead", "L-1"), Person("m1"), Group(), Link("lead", "Leader"), Link("m1"));

        var saved = await rig.Service.UpdateChannelsAsync("c1", new UpdateCommunityChannelsRequest([new("WhatsApp", "  Mining 2018 chat ", "https://chat.whatsapp.com/AbC123")]), "lead");

        Assert.Equal(200, saved.Code);
        var channel = Assert.Single(saved.Data!);
        Assert.Equal(("WhatsApp", "Mining 2018 chat", "https://chat.whatsapp.com/AbC123"), (channel.Type, channel.DisplayName, channel.InviteUrl));
        var asMember = (await rig.Fresh().GetCommunityAsync("c1", "m1")).Data!;
        Assert.Single(asMember.Channels!);
    }

    [Fact]
    public async Task Someone_who_is_not_in_the_community_never_sees_the_group_link()
    {
        var rig = new Rig();
        await rig.Seed(Person("lead", "L-1"), Person("outsider"), Group(), Link("lead", "Leader"));
        await rig.Service.UpdateChannelsAsync("c1", new UpdateCommunityChannelsRequest([new("WhatsApp", "Chat", "https://chat.whatsapp.com/AbC123")]), "lead");

        Assert.Null((await rig.Fresh().GetCommunityAsync("c1", "outsider")).Data!.Channels);
        Assert.Single((await rig.Fresh().GetCommunityAsync("c1", "lead")).Data!.Channels!);
    }

    [Theory]
    [InlineData("http://chat.whatsapp.com/AbC")]
    [InlineData("https://evil.example.com/AbC")]
    [InlineData("https://chat.whatsapp.com.evil.com/AbC")]
    [InlineData("javascript:alert(1)")]
    public async Task Only_official_whatsapp_invite_links_are_accepted(string url)
    {
        var rig = new Rig();
        await rig.Seed(Person("lead", "L-1"), Group(), Link("lead", "Leader"));

        var response = await rig.Service.UpdateChannelsAsync("c1", new UpdateCommunityChannelsRequest([new("WhatsApp", "Chat", url)]), "lead");

        Assert.Equal(400, response.Code);
        Assert.Null((await rig.Db().Communities.SingleAsync(c => c.Id == "c1")).ExternalChannels);
    }

    [Theory]
    [InlineData("", "https://chat.whatsapp.com/AbC")]
    [InlineData("Telegram group", "https://t.me/x")]
    public async Task A_group_needs_a_name_and_must_be_a_whatsapp_one(string name, string url)
    {
        var rig = new Rig();
        await rig.Seed(Person("lead", "L-1"), Group(), Link("lead", "Leader"));
        var type = name == "Telegram group" ? "Telegram" : "WhatsApp";

        Assert.Equal(400, (await rig.Service.UpdateChannelsAsync("c1", new UpdateCommunityChannelsRequest([new(type, name, url)]), "lead")).Code);
    }

    [Fact]
    public async Task Sending_an_empty_list_disconnects_the_group()
    {
        var rig = new Rig();
        await rig.Seed(Person("lead", "L-1"), Group(), Link("lead", "Leader"));
        await rig.Service.UpdateChannelsAsync("c1", new UpdateCommunityChannelsRequest([new("WhatsApp", "Chat", "https://chat.whatsapp.com/AbC")]), "lead");

        var cleared = await rig.Fresh().UpdateChannelsAsync("c1", new UpdateCommunityChannelsRequest([]), "lead");

        Assert.Equal(200, cleared.Code);
        Assert.Empty(cleared.Data!);
        Assert.Null((await rig.Db().Communities.SingleAsync(c => c.Id == "c1")).ExternalChannels);
    }

    [Fact]
    public async Task Re_saving_an_unchanged_group_keeps_who_connected_it_and_when()
    {
        var rig = new Rig();
        await rig.Seed(Person("lead", "L-1"), Group(), Link("lead", "Leader"));
        var req = new UpdateCommunityChannelsRequest([new("WhatsApp", "Chat", "https://chat.whatsapp.com/AbC")]);
        var first = (await rig.Service.UpdateChannelsAsync("c1", req, "lead")).Data!.Single().ConnectedAt;

        var second = (await rig.Fresh().UpdateChannelsAsync("c1", req, "lead")).Data!.Single().ConnectedAt;

        Assert.Equal(first, second);
    }
}
