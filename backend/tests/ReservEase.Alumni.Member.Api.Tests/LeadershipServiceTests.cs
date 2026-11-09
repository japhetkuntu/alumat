using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

/// <summary>What a member can read about leadership: who leads a group, and the titles of the people in a list.</summary>
public class LeadershipServiceTests
{
    private const string Tenant = "inst-1";
    private const string Other = "inst-2";

    private static async Task<(string Db, Func<string, LeadershipService> Service)> Rig()
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant))
        {
            db.AddRange(
                new MemberEntity { Id = "m1", InstitutionId = Tenant, FirstName = "Ama", LastName = "Boateng", Email = "a@x.test", Status = "Active", JobTitle = "Engineer" },
                new MemberEntity { Id = "m2", InstitutionId = Tenant, FirstName = "Kwame", LastName = "Owusu", Email = "k@x.test", Status = "Suspended" },
                new Batch { Id = "b1", InstitutionId = Tenant, Name = "Class of 2015", Year = 2015 },
                new LeadershipRole { Id = "r-coord", InstitutionId = Tenant, Name = "Coordinator", Privileges = [LeadershipPrivileges.ManageMembers, LeadershipPrivileges.ViewReport] },
                new LeadershipRole { Id = "r-treas", InstitutionId = Tenant, Name = "Treasurer" },
                new LeadershipRole { Id = "r-patron", InstitutionId = Tenant, Name = "Patron" },
                new LeadershipPost { Id = "p1", InstitutionId = Tenant, MemberId = "m1", RoleId = "r-coord", ScopeType = "Batch", ScopeId = "b1" },
                new LeadershipPost { Id = "p2", InstitutionId = Tenant, MemberId = "m2", RoleId = "r-treas", ScopeType = "Batch", ScopeId = "b1" },
                new LeadershipPost { Id = "p3", InstitutionId = Tenant, MemberId = "m1", RoleId = "r-patron", ScopeType = "Institution", ScopeId = "" });
            await db.SaveChangesAsync();
        }
        using (var db = TestDb.Create(name, Other))
        {
            db.AddRange(
                new MemberEntity { Id = "x1", InstitutionId = Other, FirstName = "Yaw", LastName = "Other", Email = "y@x.test", Status = "Active" },
                new LeadershipRole { Id = "r-chair", InstitutionId = Other, Name = "Chair" },
                new LeadershipPost { Id = "px", InstitutionId = Other, MemberId = "x1", RoleId = "r-chair", ScopeType = "Batch", ScopeId = "b1" });
            await db.SaveChangesAsync();
        }
        LeadershipService Make(string tenant)
        {
            var db = TestDb.Create(name, tenant);
            var posts = new AlumniPgRepository<LeadershipPost>(db);
            var roles = new AlumniPgRepository<LeadershipRole>(db);
            return new LeadershipService(posts, roles, new LeadershipAccess(posts, roles), new AlumniPgRepository<MemberEntity>(db),
                new AlumniPgRepository<Batch>(db), new AlumniPgRepository<Community>(db), new AlumniPgRepository<InstitutionAuditLogEntry>(db), NullLogger<LeadershipService>.Instance);
        }
        return (name, Make);
    }

    [Fact]
    public async Task A_leader_can_give_up_their_own_role_and_it_is_recorded_for_the_administrators()
    {
        var (name, make) = await Rig();

        var result = await make(Tenant).StepDownAsync("m1", "p1");

        Assert.Equal(200, result.Code);
        using var db = TestDb.Create(name, Tenant);
        Assert.False(await db.LeadershipPosts.AnyAsync(p => p.Id == "p1"));
        Assert.True(await db.LeadershipPosts.AnyAsync(p => p.Id == "p3"));   // their other role is untouched
        Assert.True(await db.Members.AnyAsync(m => m.Id == "m1"));
        var entry = await db.InstitutionAuditLogEntries.SingleAsync(a => a.Action == "Leader Stepped Down");
        Assert.Contains("Coordinator", entry.Target);
        Assert.Contains("Class of 2015", entry.Target);
    }

    [Fact]
    public async Task Nobody_can_give_up_someone_elses_role_or_one_in_another_community()
    {
        var (name, make) = await Rig();

        Assert.Equal(404, (await make(Tenant).StepDownAsync("m1", "p2")).Code);      // p2 is Kwame's
        Assert.Equal(404, (await make(Tenant).StepDownAsync("m1", "px")).Code);      // another community's post
        Assert.Equal(404, (await make(Tenant).StepDownAsync("m1", "nope")).Code);
        using var db = TestDb.Create(name, Tenant);
        Assert.True(await db.LeadershipPosts.AnyAsync(p => p.Id == "p2"));
    }

    [Fact]
    public async Task A_groups_leaders_are_listed_without_people_who_are_no_longer_active()
    {
        var (_, make) = await Rig();

        var result = (await make(Tenant).GetPostsAsync("Batch", "b1", null)).Data!;

        var only = Assert.Single(result);
        Assert.Equal(("Ama Boateng", "Coordinator", "Class of 2015", "Engineer"), (only.MemberName, only.Title, only.ScopeName, only.JobTitle));
    }

    [Fact]
    public async Task Titles_can_be_looked_up_for_the_people_in_a_list()
    {
        var (_, make) = await Rig();

        var result = (await make(Tenant).GetPostsAsync(null, null, ["m1", "m2", "nobody"])).Data!;

        Assert.Equal(["Coordinator", "Patron"], result.Select(p => p.Title).Order().ToArray());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("Planet", "b1")]
    public async Task Asking_for_nothing_in_particular_returns_nothing_instead_of_every_post(string? scopeType, string? scopeId)
    {
        var (_, make) = await Rig();

        Assert.Empty((await make(Tenant).GetPostsAsync(scopeType, scopeId, null)).Data!);
    }

    [Fact]
    public async Task Another_institutions_leaders_are_never_returned()
    {
        var (_, make) = await Rig();

        Assert.DoesNotContain("Chair", (await make(Tenant).GetPostsAsync("Batch", "b1", null)).Data!.Select(p => p.Title));
        Assert.Empty((await make(Tenant).GetPostsAsync(null, null, ["x1"])).Data!);
        Assert.Equal(["Chair"], (await make(Other).GetPostsAsync("Batch", "b1", null)).Data!.Select(p => p.Title).ToArray());
    }

    [Fact]
    public async Task A_member_is_told_in_plain_words_what_each_of_their_roles_allows()
    {
        var (_, make) = await Rig();

        var mine = (await make(Tenant).GetMineAsync("m1")).Data!;

        var coord = Assert.Single(mine, r => r.RoleName == "Coordinator");
        Assert.Equal(("Batch", "Class of 2015"), (coord.ScopeType, coord.ScopeName));
        Assert.Equal(["Welcome and approve members", "See the group's report"], coord.Privileges.Select(p => p.Label).ToArray());
        Assert.All(coord.Privileges, p => Assert.False(string.IsNullOrWhiteSpace(p.Description)));
        var patron = Assert.Single(mine, r => r.RoleName == "Patron");
        Assert.Empty(patron.Privileges);                     // a title only: nothing to list
        Assert.Empty((await make(Tenant).GetMineAsync("nobody")).Data!);
    }

    [Fact]
    public async Task A_members_own_roles_never_include_another_institutions()
    {
        var (_, make) = await Rig();

        Assert.Empty((await make(Tenant).GetMineAsync("x1")).Data!);
        Assert.Equal(["Chair"], (await make(Other).GetMineAsync("x1")).Data!.Select(r => r.RoleName).ToArray());
    }
}
