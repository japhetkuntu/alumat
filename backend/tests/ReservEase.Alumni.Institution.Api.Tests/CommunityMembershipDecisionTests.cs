using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>An administrator deciding who is in a group: approve or decline a request, and remove someone who was approved.</summary>
public class CommunityMembershipDecisionTests
{
    private const string Tenant = "inst-1";
    private static readonly AuthData Admin = new() { Id = "admin1", FirstName = "Kojo", LastName = "Mensah", Role = "SuperAdmin" };

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public CommunityService Service { get; private set; } = null!;
        public Mock<IInstitutionAuditLogService> Audit { get; } = new();
        public Rig() => Fresh();

        public CommunityService Fresh()
        {
            var db = TestDb.Create(DbName, Tenant);
            return Service = new CommunityService(new AlumniPgRepository<Community>(db), new AlumniPgRepository<CommunityMembership>(db),
                new AlumniPgRepository<MemberEntity>(db), new AlumniPgRepository<Referral>(db), new AlumniPgRepository<Notification>(db),
                Audit.Object, NullLogger<CommunityService>.Instance);
        }

        public AlumniDbContext Db() => TestDb.Create(DbName, Tenant);

        public async Task Seed(params object[] entities)
        {
            using var db = Db();
            foreach (var e in entities) { if (e is PostgresDb.Sdk.Entities.ITenantScoped t) t.InstitutionId = Tenant; db.Add(e); }
            await db.SaveChangesAsync();
            Fresh();
        }

        public async Task Group(params (string MemberId, string Status, string Role)[] people)
        {
            var list = new List<object> { new Community { Id = "c1", Name = "Mining 2018" } };
            foreach (var (id, status, role) in people)
            {
                list.Add(new MemberEntity { Id = id, FirstName = id, LastName = "Test", Email = $"{id}@x.test" });
                list.Add(new CommunityMembership { CommunityId = "c1", MemberId = id, Status = status, Role = role });
            }
            await Seed([.. list]);
        }
    }

    [Fact]
    public async Task Approving_a_request_lets_the_member_in_and_tells_them()
    {
        var rig = new Rig(); await rig.Group(("m1", "Pending", "Member"));

        var result = await rig.Service.DecideJoinRequestAsync("c1", "m1", true, null, Admin);

        Assert.Equal(200, result.Code);
        var m = await rig.Db().CommunityMemberships.SingleAsync();
        Assert.Equal(("Approved", "admin1"), (m.Status, m.DecidedBy));
        var n = await rig.Db().Notifications.SingleAsync();
        Assert.Equal(("m1", "Member", "/communities/c1"), (n.RecipientId, n.RecipientType, n.ActionUrl));
        rig.Audit.Verify(a => a.LogAsync(Admin, "Group Request Approved", "Mining 2018"), Times.Once);
    }

    [Fact]
    public async Task Declining_a_request_keeps_the_row_and_passes_the_reason_on()
    {
        var rig = new Rig(); await rig.Group(("m1", "Pending", "Member"));

        var result = await rig.Service.DecideJoinRequestAsync("c1", "m1", false, "  This group is for the 2018 year only.  ", Admin);

        Assert.Equal(200, result.Code);
        Assert.Equal("Rejected", (await rig.Db().CommunityMemberships.SingleAsync()).Status);
        var n = await rig.Db().Notifications.SingleAsync();
        Assert.Contains("not approved", n.Body);
        Assert.EndsWith("This group is for the 2018 year only.", n.Body);
    }

    [Fact]
    public async Task A_request_cannot_be_decided_twice()
    {
        var rig = new Rig(); await rig.Group(("m1", "Approved", "Member"), ("m2", "Rejected", "Member"));

        Assert.Equal(409, (await rig.Service.DecideJoinRequestAsync("c1", "m1", false, null, Admin)).Code);
        Assert.Equal(409, (await rig.Service.DecideJoinRequestAsync("c1", "m1", true, null, Admin)).Code);
        Assert.Equal(409, (await rig.Service.DecideJoinRequestAsync("c1", "m2", false, null, Admin)).Code);
        Assert.Equal(404, (await rig.Service.DecideJoinRequestAsync("c1", "nobody", true, null, Admin)).Code);
        Assert.Empty(await rig.Db().Notifications.ToListAsync());
    }

    [Fact]
    public async Task A_declined_request_can_still_be_approved_if_the_administrator_changes_their_mind()
    {
        var rig = new Rig(); await rig.Group(("m2", "Rejected", "Member"));

        Assert.Equal(200, (await rig.Service.DecideJoinRequestAsync("c1", "m2", true, null, Admin)).Code);
        Assert.Equal("Approved", (await rig.Db().CommunityMemberships.SingleAsync()).Status);
    }

    [Fact]
    public async Task Removing_a_member_takes_them_out_tells_them_and_lets_them_ask_again()
    {
        var rig = new Rig(); await rig.Group(("m1", "Approved", "Member"), ("m2", "Approved", "Member"));

        var result = await rig.Service.RemoveMemberAsync("c1", "m1", "Moved to another chapter.", Admin);

        Assert.Equal(200, result.Code);
        var left = await rig.Db().CommunityMemberships.ToListAsync();
        Assert.Equal("m2", left.Single().MemberId);   // no row is left behind, so a fresh request is a clean start
        var n = await rig.Db().Notifications.SingleAsync();
        Assert.Equal("m1", n.RecipientId);
        Assert.EndsWith("Moved to another chapter.", n.Body);
        rig.Audit.Verify(a => a.LogAsync(Admin, "Group Member Removed", "Mining 2018"), Times.Once);
    }

    [Fact]
    public async Task A_leader_can_be_removed_and_only_approved_members_can()
    {
        var rig = new Rig(); await rig.Group(("lead", "Approved", "Leader"), ("wait", "Pending", "Member"));

        Assert.Equal(404, (await rig.Service.RemoveMemberAsync("c1", "wait", null, Admin)).Code);   // a pending request is declined, not removed
        Assert.Equal(200, (await rig.Service.RemoveMemberAsync("c1", "lead", null, Admin)).Code);
        Assert.Equal(404, (await rig.Service.RemoveMemberAsync("c1", "lead", null, Admin)).Code);   // already gone
        Assert.Equal("wait", (await rig.Db().CommunityMemberships.SingleAsync()).MemberId);
    }

    [Fact]
    public async Task A_missing_member_record_does_not_stop_the_decision()
    {
        var rig = new Rig();
        await rig.Seed(new Community { Id = "c1", Name = "Mining 2018" }, new CommunityMembership { CommunityId = "c1", MemberId = "ghost", Status = "Pending" });

        Assert.Equal(200, (await rig.Service.DecideJoinRequestAsync("c1", "ghost", true, null, Admin)).Code);
        Assert.Empty(await rig.Db().Notifications.ToListAsync());   // no one to tell
    }
}
