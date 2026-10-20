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
    public async Task Only_an_approved_leader_can_see_invitation_details(string role, string status)
    {
        var rig = new Rig();
        await rig.Seed(Person("x", "X-1"), Group(), role == "none" ? new Community { Id = "other", Name = "Other" } : Link("x", role, status));

        Assert.Equal(404, (await rig.Service.GetInviteInfoAsync("c1", "x")).Code);
    }
}
