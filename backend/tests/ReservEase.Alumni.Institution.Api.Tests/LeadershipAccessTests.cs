using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>The rule behind every leader's action: a privilege holds only inside the group the role was given for, and a whole-institution role holds everywhere.</summary>
public class LeadershipAccessTests
{
    private const string Tenant = "inst-1";
    private const string Other = "inst-2";
    private const string Members = LeadershipPrivileges.ManageMembers;
    private const string Report = LeadershipPrivileges.ViewReport;

    private static async Task<(string Db, Func<string, LeadershipAccess> Access, Func<string, AlumniDbContext> Open)> Rig()
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant))
        {
            db.AddRange(
                new LeadershipRole { Id = "welcome", InstitutionId = Tenant, Name = "Welcomer", Privileges = [Members] },
                new LeadershipRole { Id = "both", InstitutionId = Tenant, Name = "Convener", Privileges = [Members, Report] },
                new LeadershipRole { Id = "title", InstitutionId = Tenant, Name = "Patron", Privileges = [] },
                new LeadershipRole { Id = "stale", InstitutionId = Tenant, Name = "Old", Privileges = ["NotARealPrivilege", Report] },
                new LeadershipPost { Id = "p1", InstitutionId = Tenant, MemberId = "ama", RoleId = "welcome", ScopeType = "Batch", ScopeId = "b2015" },
                new LeadershipPost { Id = "p2", InstitutionId = Tenant, MemberId = "kojo", RoleId = "both", ScopeType = "Institution", ScopeId = "" },
                new LeadershipPost { Id = "p3", InstitutionId = Tenant, MemberId = "esi", RoleId = "title", ScopeType = "Community", ScopeId = "c1" },
                new LeadershipPost { Id = "p4", InstitutionId = Tenant, MemberId = "yaa", RoleId = "stale", ScopeType = "Batch", ScopeId = "b2015" });
            await db.SaveChangesAsync();
        }
        using (var db = TestDb.Create(name, Other))
        {
            db.AddRange(
                new LeadershipRole { Id = "xr", InstitutionId = Other, Name = "Chair", Privileges = [Members] },
                new LeadershipPost { Id = "px", InstitutionId = Other, MemberId = "ama", RoleId = "xr", ScopeType = "Institution", ScopeId = "" });
            await db.SaveChangesAsync();
        }
        AlumniDbContext Open(string tenant) => TestDb.Create(name, tenant);
        LeadershipAccess Make(string tenant)
        {
            var db = Open(tenant);
            return new LeadershipAccess(new AlumniPgRepository<LeadershipPost>(db), new AlumniPgRepository<LeadershipRole>(db));
        }
        return (name, Make, Open);
    }

    [Fact]
    public async Task A_privilege_holds_in_the_group_the_role_was_given_for_and_nowhere_else()
    {
        var (_, make, _) = await Rig();
        var access = make(Tenant);

        Assert.True(await access.HasPrivilegeAsync("ama", Members, "Batch", "b2015"));
        Assert.False(await access.HasPrivilegeAsync("ama", Members, "Batch", "b2016"));         // another year group
        Assert.False(await access.HasPrivilegeAsync("ama", Members, "Community", "b2015"));     // same id, different kind of group
        Assert.False(await access.HasPrivilegeAsync("ama", Report, "Batch", "b2015"));          // not a privilege of her role
    }

    [Fact]
    public async Task A_role_for_the_whole_institution_holds_in_every_group_but_only_for_what_it_allows()
    {
        var (_, make, _) = await Rig();
        var access = make(Tenant);

        Assert.True(await access.HasPrivilegeAsync("kojo", Members, "Batch", "anything"));
        Assert.True(await access.HasPrivilegeAsync("kojo", Report, "Community", "c9"));
        Assert.False(await access.HasPrivilegeAsync("kojo", LeadershipPrivileges.MessageGroup, "Batch", "anything"));
    }

    [Fact]
    public async Task A_title_with_no_privileges_and_a_member_with_no_role_may_do_nothing()
    {
        var (_, make, _) = await Rig();
        var access = make(Tenant);

        foreach (var p in LeadershipPrivileges.All)
        {
            Assert.False(await access.HasPrivilegeAsync("esi", p.Key, "Community", "c1"));
            Assert.False(await access.HasPrivilegeAsync("nobody", p.Key, "Community", "c1"));
        }
    }

    [Fact]
    public async Task A_privilege_the_platform_does_not_know_is_never_honoured()
    {
        var (_, make, _) = await Rig();
        var access = make(Tenant);

        Assert.False(await access.HasPrivilegeAsync("yaa", "NotARealPrivilege", "Batch", "b2015"));
        Assert.True(await access.HasPrivilegeAsync("yaa", Report, "Batch", "b2015"));
        Assert.Equal([Report], (await access.GetHeldAsync("yaa")).Single().Privileges);
    }

    [Fact]
    public async Task Editing_a_role_or_removing_a_post_takes_effect_on_the_next_check()
    {
        var (_, make, open) = await Rig();
        Assert.True(await make(Tenant).HasPrivilegeAsync("ama", Members, "Batch", "b2015"));

        using (var db = open(Tenant))
        {
            db.LeadershipRoles.Single(r => r.Id == "welcome").Privileges = [Report];
            await db.SaveChangesAsync();
        }
        Assert.False(await make(Tenant).HasPrivilegeAsync("ama", Members, "Batch", "b2015"));
        Assert.True(await make(Tenant).HasPrivilegeAsync("ama", Report, "Batch", "b2015"));

        using (var db = open(Tenant))
        {
            db.LeadershipPosts.Remove(db.LeadershipPosts.Single(p => p.Id == "p1"));
            await db.SaveChangesAsync();
        }
        Assert.False(await make(Tenant).HasPrivilegeAsync("ama", Report, "Batch", "b2015"));
    }

    [Fact]
    public async Task A_role_in_another_institution_never_counts_even_for_the_same_member_id()
    {
        var (_, make, _) = await Rig();

        // "ama" is a Chair with ManageMembers over the whole of institution 2, and a Welcomer for one year group in institution 1.
        Assert.True(await make(Other).HasPrivilegeAsync("ama", Members, "Batch", "b2099"));
        Assert.False(await make(Tenant).HasPrivilegeAsync("ama", Members, "Batch", "b2099"));
        Assert.Equal(["Welcomer"], (await make(Tenant).GetHeldAsync("ama")).Select(h => h.RoleName).ToArray());
    }
}
