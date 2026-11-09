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
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>Roles an institution makes for its leaders, and who holds which role where: creating, changing, appointing, removing, and staying inside one institution.</summary>
public class LeadershipServiceTests
{
    private const string Tenant = "inst-1";
    private const string Other = "inst-2";
    private static readonly AuthData Admin = new() { Id = "admin1", FirstName = "Kojo", LastName = "Mensah", Role = "SuperAdmin" };

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public Mock<IInstitutionAuditLogService> Audit { get; } = new();

        public LeadershipService Service(string tenant = Tenant)
        {
            var db = TestDb.Create(DbName, tenant);
            return new LeadershipService(new AlumniPgRepository<LeadershipPost>(db), new AlumniPgRepository<LeadershipRole>(db),
                new AlumniPgRepository<MemberEntity>(db), new AlumniPgRepository<Batch>(db), new AlumniPgRepository<Community>(db),
                new AlumniPgRepository<Notification>(db), new AlumniPgRepository<PostgresDb.Sdk.Entities.Institution>(db), TestDb.Tenant(tenant, "umat"), Audit.Object, NullLogger<LeadershipService>.Instance);
        }

        public AlumniDbContext Db(string tenant = Tenant) => TestDb.Create(DbName, tenant);

        public async Task Seed(string tenant, params object[] entities)
        {
            using var db = Db(tenant);
            foreach (var e in entities) { if (e is PostgresDb.Sdk.Entities.ITenantScoped t) t.InstitutionId = tenant; db.Add(e); }
            await db.SaveChangesAsync();
        }

        public Task Basics() => Seed(Tenant,
            new MemberEntity { Id = "m1", FirstName = "Ama", LastName = "Boateng", Email = "ama@x.test", Status = "Active", GraduationYear = 2015 },
            new MemberEntity { Id = "m2", FirstName = "Kwame", LastName = "Owusu", Email = "kwame@x.test", Status = "Active" },
            new MemberEntity { Id = "m3", FirstName = "Esi", LastName = "Pending", Email = "esi@x.test", Status = "Pending" },
            new Batch { Id = "b2015", Name = "Class of 2015", Year = 2015 },
            new Community { Id = "c1", Name = "Greater Accra Chapter" },
            new LeadershipRole { Id = "coord", Name = "Coordinator", Privileges = [LeadershipPrivileges.ManageMembers, LeadershipPrivileges.MessageGroup] },
            new LeadershipRole { Id = "patron", Name = "Patron" });
    }

    private static SaveLeadershipRoleRequest Role(string name, params string[] privileges) => new() { Name = name, Privileges = [.. privileges] };
    private static AppointLeaderRequest Req(string member, string role, string scope = "Batch", string? id = "b2015")
        => new() { MemberId = member, RoleId = role, ScopeType = scope, ScopeId = id };

    // ── How often leaders may message ────────────────────────────────────────

    [Fact]
    public async Task The_limit_starts_at_three_and_the_institution_can_change_it_with_a_trail()
    {
        var rig = new Rig();
        await rig.Seed(Tenant, new PostgresDb.Sdk.Entities.Institution { Id = Tenant, Name = "UMaT", Slug = "umat" });

        Assert.Equal(3, (await rig.Service().GetSettingsAsync()).Data!.GroupMessagesPerDay);
        var saved = await rig.Service().UpdateSettingsAsync(new SaveLeadershipSettingsRequest { GroupMessagesPerDay = 6 }, Admin);

        Assert.Equal(200, saved.Code);
        Assert.Equal(6, (await rig.Service().GetSettingsAsync()).Data!.GroupMessagesPerDay);
        rig.Audit.Verify(a => a.LogAsync(Admin, "Leader Message Limit Changed", "3 to 6 a day"), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-1)]
    public async Task A_limit_outside_one_to_ten_is_refused_and_changes_nothing(int value)
    {
        var rig = new Rig();
        await rig.Seed(Tenant, new PostgresDb.Sdk.Entities.Institution { Id = Tenant, Name = "UMaT", Slug = "umat" });

        Assert.Equal(400, (await rig.Service().UpdateSettingsAsync(new SaveLeadershipSettingsRequest { GroupMessagesPerDay = value }, Admin)).Code);
        Assert.Equal(3, (await rig.Service().GetSettingsAsync()).Data!.GroupMessagesPerDay);
    }

    [Fact]
    public async Task Saving_the_same_limit_leaves_no_audit_entry()
    {
        var rig = new Rig();
        await rig.Seed(Tenant, new PostgresDb.Sdk.Entities.Institution { Id = Tenant, Name = "UMaT", Slug = "umat" });

        await rig.Service().UpdateSettingsAsync(new SaveLeadershipSettingsRequest { GroupMessagesPerDay = 3 }, Admin);

        rig.Audit.Verify(a => a.LogAsync(It.IsAny<AuthData>(), "Leader Message Limit Changed", It.IsAny<string>()), Times.Never);
    }

    // ── The list of things a role can allow ──────────────────────────────────

    [Fact]
    public void The_offered_privileges_are_exactly_the_ones_that_have_a_working_screen_and_nothing_about_money_settings_or_staff()
    {
        var keys = LeadershipPrivileges.All.Select(p => p.Key).ToArray();

        Assert.Equal(["ManageMembers", "MessageGroup", "ManageEvents", "PostNews", "PostJobs", "ShareResources", "ManageFundraisers", "ViewGiving", "ViewReport"], keys);
        Assert.All(LeadershipPrivileges.All, p => Assert.False(string.IsNullOrWhiteSpace(p.Label) || string.IsNullOrWhiteSpace(p.Description)));
        Assert.DoesNotContain(keys, k => k.Contains("Payout") || k.Contains("Setting") || k.Contains("Staff") || k.Contains("Role"));
    }

    // ── Roles ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Creating_a_role_keeps_the_name_the_institution_chose_and_lists_privileges_in_the_offered_order()
    {
        var rig = new Rig();

        var result = await rig.Service().CreateRoleAsync(Role("  Chapter   Convener ", "ViewReport", "ManageMembers", "ViewReport"), Admin);

        Assert.Equal(200, result.Code);
        Assert.Equal("Chapter Convener", result.Data!.Name);
        Assert.Equal(["ManageMembers", "ViewReport"], result.Data.Privileges);
        var saved = await rig.Db().LeadershipRoles.SingleAsync();
        Assert.Equal((Tenant, "admin1"), (saved.InstitutionId, saved.CreatedBy));
        rig.Audit.Verify(a => a.LogAsync(Admin, "Leadership Role Created", It.Is<string>(t => t.Contains("Chapter Convener") && t.Contains("Welcome and approve members"))), Times.Once);
    }

    [Fact]
    public async Task A_role_with_no_privileges_is_recognition_only()
    {
        var rig = new Rig();

        var result = await rig.Service().CreateRoleAsync(Role("Patron"), Admin);

        Assert.Equal(200, result.Code);
        Assert.Empty(result.Data!.Privileges);
    }

    [Theory]
    [InlineData("   ", new string[0], "Give the role a name")]
    [InlineData("Treasurer", new[] { "ManagePayouts" }, "not recognised")]
    [InlineData("Treasurer", new[] { "ManageMembers", "EditSettings" }, "not recognised")]
    public async Task A_role_with_no_name_or_an_unknown_privilege_is_refused(string name, string[] privileges, string message)
    {
        var rig = new Rig();

        var result = await rig.Service().CreateRoleAsync(Role(name, privileges), Admin);

        Assert.Equal(400, result.Code);
        Assert.Contains(message, result.Message);
        Assert.Equal(0, await rig.Db().LeadershipRoles.CountAsync());
    }

    [Fact]
    public async Task Two_roles_cannot_share_a_name_whatever_its_case_but_two_institutions_can()
    {
        var rig = new Rig();
        await rig.Service().CreateRoleAsync(Role("Treasurer"), Admin);

        Assert.Equal(400, (await rig.Service().CreateRoleAsync(Role("TREASURER", "ViewReport"), Admin)).Code);
        Assert.Equal(200, (await rig.Service(Other).CreateRoleAsync(Role("Treasurer"), Admin)).Code);
    }

    [Fact]
    public async Task Changing_a_role_changes_it_for_everyone_who_holds_it()
    {
        var rig = new Rig(); await rig.Basics();
        await rig.Service().AppointAsync(Req("m1", "coord"), Admin);
        await rig.Service().AppointAsync(Req("m2", "coord"), Admin);

        var result = await rig.Service().UpdateRoleAsync("coord", Role("Class Coordinator", "ViewReport"), Admin);

        Assert.Equal(200, result.Code);
        Assert.Equal(("Class Coordinator", 2), (result.Data!.Name, result.Data.HolderCount));
        var posts = (await rig.Service().GetPostsAsync("Batch", "b2015", null)).Data!;
        Assert.All(posts, p => { Assert.Equal("Class Coordinator", p.Title); Assert.Equal(["ViewReport"], p.Privileges); });
        rig.Audit.Verify(a => a.LogAsync(Admin, "Leadership Role Updated", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task A_role_cannot_be_renamed_to_another_roles_name_and_a_missing_role_is_not_found()
    {
        var rig = new Rig(); await rig.Basics();

        Assert.Equal(400, (await rig.Service().UpdateRoleAsync("coord", Role("patron"), Admin)).Code);
        Assert.Equal(200, (await rig.Service().UpdateRoleAsync("coord", Role("COORDINATOR", "ViewReport"), Admin)).Code);   // its own name again is fine
        Assert.Equal(404, (await rig.Service().UpdateRoleAsync("nope", Role("X"), Admin)).Code);
    }

    [Fact]
    public async Task A_role_someone_still_holds_cannot_be_deleted_but_an_unused_one_can()
    {
        var rig = new Rig(); await rig.Basics();
        await rig.Service().AppointAsync(Req("m1", "coord"), Admin);

        var held = await rig.Service().DeleteRoleAsync("coord", Admin);
        Assert.Equal(400, held.Code);
        Assert.Contains("1 person holds", held.Message);
        Assert.Equal(2, await rig.Db().LeadershipRoles.CountAsync());

        Assert.Equal(200, (await rig.Service().DeleteRoleAsync("patron", Admin)).Code);
        Assert.Equal(1, await rig.Db().LeadershipRoles.CountAsync());
        rig.Audit.Verify(a => a.LogAsync(Admin, "Leadership Role Deleted", "Patron"), Times.Once);
    }

    [Fact]
    public async Task Roles_are_listed_by_name_with_how_many_people_hold_each()
    {
        var rig = new Rig(); await rig.Basics();
        await rig.Service().AppointAsync(Req("m1", "coord"), Admin);
        await rig.Service().AppointAsync(Req("m2", "coord", "Community", "c1"), Admin);

        var roles = (await rig.Service().GetRolesAsync()).Data!;

        Assert.Equal([("Coordinator", 2), ("Patron", 0)], roles.Select(r => (r.Name, r.HolderCount)).ToArray());
    }

    // ── Appointing ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Appointing_a_member_records_the_post_and_the_audit_entry_with_what_it_allows()
    {
        var rig = new Rig(); await rig.Basics();

        var result = await rig.Service().AppointAsync(Req("m1", "coord"), Admin);

        Assert.Equal(200, result.Code);
        Assert.Equal(("Ama Boateng", "Coordinator", "Class of 2015"), (result.Data!.MemberName, result.Data.Title, result.Data.ScopeName));
        Assert.Equal(["ManageMembers", "MessageGroup"], result.Data.Privileges);
        var post = await rig.Db().LeadershipPosts.SingleAsync();
        Assert.Equal(("m1", "coord", "Batch", "b2015", "admin1", Tenant), (post.MemberId, post.RoleId, post.ScopeType, post.ScopeId, post.CreatedBy, post.InstitutionId));
        rig.Audit.Verify(a => a.LogAsync(Admin, "Leader Appointed", It.Is<string>(t => t.Contains("Ama Boateng") && t.Contains("Coordinator") && t.Contains("Class of 2015") && t.Contains("Message the whole group"))), Times.Once);
    }

    [Fact]
    public async Task The_same_person_can_hold_several_roles_in_different_places()
    {
        var rig = new Rig(); await rig.Basics();
        var s = rig.Service();

        Assert.Equal(200, (await s.AppointAsync(Req("m1", "coord"), Admin)).Code);
        Assert.Equal(200, (await s.AppointAsync(Req("m1", "patron"), Admin)).Code);
        Assert.Equal(200, (await s.AppointAsync(Req("m1", "coord", "Community", "c1"), Admin)).Code);
        Assert.Equal(200, (await s.AppointAsync(Req("m1", "patron", "Institution", null), Admin)).Code);

        var mine = (await rig.Service().GetPostsAsync(null, null, "m1")).Data!;
        Assert.Equal(4, mine.Count);
    }

    [Fact]
    public async Task The_same_role_twice_for_the_same_person_and_place_is_refused()
    {
        var rig = new Rig(); await rig.Basics();
        await rig.Service().AppointAsync(Req("m1", "coord"), Admin);

        var again = await rig.Service().AppointAsync(Req("m1", "coord"), Admin);

        Assert.Equal(400, again.Code);
        Assert.Equal(1, await rig.Db().LeadershipPosts.CountAsync());
    }

    [Theory]
    [InlineData("m3", "coord", "Batch", "b2015", 404)]      // not an active member
    [InlineData("nobody", "coord", "Batch", "b2015", 404)]
    [InlineData("m1", "missing", "Batch", "b2015", 404)]    // no such role
    [InlineData("m1", "coord", "Batch", "missing", 404)]    // the group does not exist
    [InlineData("m1", "coord", "Planet", "b2015", 400)]     // not a kind of group
    public async Task Bad_appointments_are_refused_and_nothing_is_saved(string member, string role, string scope, string scopeId, int code)
    {
        var rig = new Rig(); await rig.Basics();

        var result = await rig.Service().AppointAsync(Req(member, role, scope, scopeId), Admin);

        Assert.Equal(code, result.Code);
        Assert.Equal(0, await rig.Db().LeadershipPosts.CountAsync());
        rig.Audit.Verify(a => a.LogAsync(It.IsAny<AuthData>(), "Leader Appointed", It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Listing_by_group_returns_only_that_groups_leaders_oldest_first()
    {
        var rig = new Rig(); await rig.Basics();
        await rig.Seed(Tenant, new Batch { Id = "b2016", Name = "Class of 2016", Year = 2016 });
        var s = rig.Service();
        await s.AppointAsync(Req("m1", "coord"), Admin);
        await s.AppointAsync(Req("m2", "patron"), Admin);
        await s.AppointAsync(Req("m2", "coord", "Batch", "b2016"), Admin);

        var result = (await rig.Service().GetPostsAsync("Batch", "b2015", null)).Data!;

        Assert.Equal(["Coordinator", "Patron"], result.Select(p => p.Title).ToArray());
    }

    [Fact]
    public async Task Removing_a_post_deletes_it_and_records_who_held_it()
    {
        var rig = new Rig(); await rig.Basics();
        var id = (await rig.Service().AppointAsync(Req("m1", "coord"), Admin)).Data!.Id;

        var result = await rig.Service().RemoveAsync(id, Admin);

        Assert.Equal(200, result.Code);
        Assert.Equal(0, await rig.Db().LeadershipPosts.CountAsync());
        Assert.Equal(1, await rig.Db().LeadershipRoles.CountAsync(r => r.Id == "coord"));   // the role stays for the next person
        rig.Audit.Verify(a => a.LogAsync(Admin, "Leader Removed", It.Is<string>(t => t.Contains("Ama Boateng") && t.Contains("Coordinator"))), Times.Once);
        Assert.Equal(404, (await rig.Service().RemoveAsync(id, Admin)).Code);   // already gone
    }

    // ── Staying inside one institution ───────────────────────────────────────

    [Fact]
    public async Task One_institution_cannot_see_use_change_or_delete_anothers_roles_and_posts()
    {
        var rig = new Rig(); await rig.Basics();
        await rig.Seed(Other, new MemberEntity { Id = "x1", FirstName = "Yaw", LastName = "Other", Email = "yaw@x.test", Status = "Active" },
            new Batch { Id = "bx", Name = "Their class", Year = 2015 }, new LeadershipRole { Id = "xrole", Name = "Chair", Privileges = ["ViewReport"] });
        var theirs = (await rig.Service(Other).AppointAsync(new AppointLeaderRequest { MemberId = "x1", RoleId = "xrole", ScopeType = "Batch", ScopeId = "bx" }, Admin)).Data!;
        var ours = (await rig.Service().AppointAsync(Req("m1", "coord"), Admin)).Data!;

        Assert.Equal([ours.Id], (await rig.Service().GetPostsAsync(null, null, null)).Data!.Select(p => p.Id).ToArray());
        Assert.Equal([theirs.Id], (await rig.Service(Other).GetPostsAsync(null, null, null)).Data!.Select(p => p.Id).ToArray());
        Assert.DoesNotContain("Chair", (await rig.Service().GetRolesAsync()).Data!.Select(r => r.Name));
        Assert.Equal(404, (await rig.Service().AppointAsync(Req("x1", "coord"), Admin)).Code);                    // their member
        Assert.Equal(404, (await rig.Service().AppointAsync(Req("m1", "coord", "Batch", "bx"), Admin)).Code);     // their group
        Assert.Equal(404, (await rig.Service().AppointAsync(Req("m1", "xrole"), Admin)).Code);                    // their role
        Assert.Equal(404, (await rig.Service().RemoveAsync(theirs.Id, Admin)).Code);
        Assert.Equal(404, (await rig.Service().UpdateRoleAsync("xrole", Role("Hijacked", "ManageMembers"), Admin)).Code);
        Assert.Equal(404, (await rig.Service().DeleteRoleAsync("xrole", Admin)).Code);
        Assert.Equal(["ViewReport"], (await rig.Db(Other).LeadershipRoles.SingleAsync()).Privileges);
    }

    // ── Telling the member ───────────────────────────────────────────────────

    [Fact]
    public async Task An_appointment_tells_the_member_in_plain_words_what_they_can_now_do()
    {
        var rig = new Rig(); await rig.Basics();

        await rig.Service().AppointAsync(Req("m1", "coord"), Admin);

        var n = await rig.Db().Notifications.SingleAsync();
        Assert.Equal(("m1", "Member", "You're now Coordinator", "/manage"), (n.RecipientId, n.RecipientType, n.Title, n.ActionUrl));
        Assert.Contains("for Class of 2015", n.Body);
        Assert.Contains("welcome and approve members and message the whole group", n.Body);
        Assert.Contains("in that group", n.Body);
    }

    [Fact]
    public async Task A_title_with_no_privileges_says_so_and_sends_the_member_to_their_profile()
    {
        var rig = new Rig(); await rig.Basics();

        await rig.Service().AppointAsync(Req("m2", "patron", "Institution", null), Admin);

        var n = await rig.Db().Notifications.SingleAsync();
        Assert.Equal("/profile", n.ActionUrl);
        Assert.Contains("for the whole community", n.Body);
        Assert.Contains("doesn't change what you can access", n.Body);
    }

    [Fact]
    public async Task A_failed_appointment_tells_nobody_and_removal_says_thank_you()
    {
        var rig = new Rig(); await rig.Basics();

        await rig.Service().AppointAsync(Req("m3", "coord"), Admin);   // not an active member
        Assert.Equal(0, await rig.Db().Notifications.CountAsync());

        var id = (await rig.Service().AppointAsync(Req("m1", "coord"), Admin)).Data!.Id;
        await rig.Service().RemoveAsync(id, Admin);

        var notes = await rig.Db().Notifications.OrderBy(x => x.CreatedAt).ToListAsync();
        Assert.Equal(2, notes.Count);
        Assert.Equal("You're no longer Coordinator for Class of 2015", notes[1].Title);
        Assert.Contains("Thank you", notes[1].Body);
        Assert.All(notes, x => Assert.Equal("m1", x.RecipientId));
    }

    // ── Changing someone's role ──────────────────────────────────────────────

    [Fact]
    public async Task A_leaders_role_can_be_changed_in_place_and_they_are_told_what_it_means()
    {
        var rig = new Rig(); await rig.Basics();
        var id = (await rig.Service().AppointAsync(Req("m1", "coord"), Admin)).Data!.Id;

        var result = await rig.Service().ChangeRoleAsync(id, "patron", Admin);

        Assert.Equal(200, result.Code);
        Assert.Equal(("Patron", 0), (result.Data!.Title, result.Data.Privileges.Count));
        var post = await rig.Db().LeadershipPosts.SingleAsync();
        Assert.Equal((id, "patron", "m1", "Batch", "b2015", "admin1"), (post.Id, post.RoleId, post.MemberId, post.ScopeType, post.ScopeId, post.UpdatedBy));
        var note = (await rig.Db().Notifications.OrderBy(n => n.CreatedAt).ToListAsync()).Last();
        Assert.Equal(("You're now Patron", "/profile"), (note.Title, note.ActionUrl));
        Assert.Contains("from Coordinator to Patron", note.Body);
        Assert.Contains("doesn't give you any extra access", note.Body);
        rig.Audit.Verify(a => a.LogAsync(Admin, "Leader Role Changed", It.Is<string>(t => t.Contains("Ama Boateng") && t.Contains("Coordinator -> Patron") && t.Contains("Class of 2015"))), Times.Once);
    }

    [Fact]
    public async Task Moving_to_a_role_with_privileges_says_what_they_can_now_do()
    {
        var rig = new Rig(); await rig.Basics();
        var id = (await rig.Service().AppointAsync(Req("m1", "patron"), Admin)).Data!.Id;

        await rig.Service().ChangeRoleAsync(id, "coord", Admin);

        var note = (await rig.Db().Notifications.OrderBy(n => n.CreatedAt).ToListAsync()).Last();
        Assert.Equal("/manage", note.ActionUrl);
        Assert.Contains("You can now welcome and approve members", note.Body);
        Assert.Contains("in that group", note.Body);
    }

    [Fact]
    public async Task Choosing_the_role_they_already_have_changes_nothing_and_tells_nobody()
    {
        var rig = new Rig(); await rig.Basics();
        var id = (await rig.Service().AppointAsync(Req("m1", "coord"), Admin)).Data!.Id;

        Assert.Equal(200, (await rig.Service().ChangeRoleAsync(id, "coord", Admin)).Code);

        Assert.Equal(1, await rig.Db().Notifications.CountAsync());   // only the original appointment
        rig.Audit.Verify(a => a.LogAsync(Admin, "Leader Role Changed", It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task A_role_cannot_be_changed_to_one_the_person_already_holds_in_that_place()
    {
        var rig = new Rig(); await rig.Basics();
        var first = (await rig.Service().AppointAsync(Req("m1", "coord"), Admin)).Data!.Id;
        await rig.Service().AppointAsync(Req("m1", "patron"), Admin);

        var result = await rig.Service().ChangeRoleAsync(first, "patron", Admin);

        Assert.Equal(400, result.Code);
        Assert.Contains("already holds Patron", result.Message);
        Assert.Equal(["coord", "patron"], (await rig.Db().LeadershipPosts.Select(p => p.RoleId).ToListAsync()).Order().ToArray());
    }

    [Fact]
    public async Task Changing_to_a_missing_role_or_a_missing_appointment_is_not_found()
    {
        var rig = new Rig(); await rig.Basics();
        var id = (await rig.Service().AppointAsync(Req("m1", "coord"), Admin)).Data!.Id;

        Assert.Equal(404, (await rig.Service().ChangeRoleAsync(id, "nope", Admin)).Code);
        Assert.Equal(404, (await rig.Service().ChangeRoleAsync("nope", "patron", Admin)).Code);
        Assert.Equal("coord", (await rig.Db().LeadershipPosts.SingleAsync()).RoleId);
    }

    [Fact]
    public async Task One_institution_cannot_change_anothers_appointments_or_use_its_roles()
    {
        var rig = new Rig(); await rig.Basics();
        await rig.Seed(Other, new MemberEntity { Id = "x1", FirstName = "Yaw", LastName = "Other", Email = "yaw@x.test", Status = "Active" },
            new Batch { Id = "bx", Name = "Theirs", Year = 2015 }, new LeadershipRole { Id = "xrole", Name = "Chair" }, new LeadershipRole { Id = "xrole2", Name = "Deputy" });
        var theirs = (await rig.Service(Other).AppointAsync(new AppointLeaderRequest { MemberId = "x1", RoleId = "xrole", ScopeType = "Batch", ScopeId = "bx" }, Admin)).Data!;
        var ours = (await rig.Service().AppointAsync(Req("m1", "coord"), Admin)).Data!;

        Assert.Equal(404, (await rig.Service().ChangeRoleAsync(theirs.Id, "patron", Admin)).Code);   // their appointment
        Assert.Equal(404, (await rig.Service().ChangeRoleAsync(ours.Id, "xrole2", Admin)).Code);      // their role
        Assert.Equal("xrole", (await rig.Db(Other).LeadershipPosts.SingleAsync()).RoleId);
        Assert.Equal("coord", (await rig.Db().LeadershipPosts.SingleAsync()).RoleId);
    }
}
