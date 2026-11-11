using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

/// <summary>What a leader may do, and above all what they may not: a privilege holds only in the group the role was given for, in their own institution.</summary>
public class ManageServiceTests
{
    private const string Tenant = "inst-1";
    private const string Other = "inst-2";

    private const string Members = LeadershipPrivileges.ManageMembers;
    private const string Message = LeadershipPrivileges.MessageGroup;
    private const string Events = LeadershipPrivileges.ManageEvents;
    private const string Report = LeadershipPrivileges.ViewReport;

    private sealed class Rig(bool email = false, bool sms = false)
    {
        public string DbName { get; } = TestDb.NewName();
        public CapturingLogger<ManageService> Log { get; } = new();
        public FakeStorage Storage { get; } = new();

        public ManageService Service(string tenant = Tenant)
        {
            var db = TestDb.Create(DbName, tenant);
            var posts = new AlumniPgRepository<LeadershipPost>(db);
            var roles = new AlumniPgRepository<LeadershipRole>(db);
            var temporal = new Mock<ITemporalClientProvider>();
            temporal.SetupGet(t => t.IsAvailable).Returns(false);   // queued notices are logged as dropped, which the tests can count by kind
            var access = new LeadershipAccess(posts, roles);
            return new ManageService(access, new GroupGuard(access, new AlumniPgRepository<Batch>(db), new AlumniPgRepository<Community>(db)), Storage.Media(TestDb.Tenant(tenant, "umat")),
                new AlumniPgRepository<MemberEntity>(db), new AlumniPgRepository<Batch>(db),
                new AlumniPgRepository<Community>(db), new AlumniPgRepository<CommunityMembership>(db), new AlumniPgRepository<Notification>(db),
                new AlumniPgRepository<AlumniEvent>(db), new AlumniPgRepository<EventRsvp>(db), new AlumniPgRepository<Institution>(db),
                new AlumniPgRepository<InstitutionAuditLogEntry>(db), TestDb.Tenant(tenant, "umat"), temporal.Object,
                new ConfigurationBuilder().Build(), Log);
        }

        public int Queued(string kind) => Log.Entries.Count(e => e.Message.Contains($"dropped notification {kind} "));

        public AlumniDbContext Db(string tenant = Tenant) => TestDb.Create(DbName, tenant);

        private static MemberEntity Person(string id, int year = 0, string status = "Active", bool complete = false) => new()
        {
            Id = id, InstitutionId = Tenant, FirstName = id, LastName = "Test", Email = $"{id}@x.test", Status = status, GraduationYear = year,
            ProfilePictureUrl = complete ? "p.jpg" : null, JobTitle = complete ? "Engineer" : null, Location = complete ? "Accra" : null,
            Bio = complete ? "Hi" : null, LinkedInUrl = complete ? "li" : null, Company = complete ? "Acme" : null,
            LastLoginAt = complete ? DateTime.UtcNow.AddDays(-2) : null,
        };

        /// <summary>
        /// Class of 2015: ama (leader, can manage members), ben (complete profile, recent), cy (empty profile), dee (pending). Class of 2016: eli.
        /// Chapter c1: ben and cy approved, fay waiting. Chapter c2: eli. Roles: ama welcomes b2015; kojo messages and reports for c1;
        /// esi holds all three for the whole institution; yaa has a title that allows nothing.
        /// </summary>
        public async Task Seed()
        {
            using var db = Db();
            db.AddRange(
                new Institution { Id = Tenant, Name = "UMaT", Slug = "umat", BroadcastEmailAllowed = email, BroadcastSmsAllowed = sms },
                new Batch { Id = "b2015", InstitutionId = Tenant, Name = "Class of 2015", Year = 2015 },
                new Batch { Id = "b2016", InstitutionId = Tenant, Name = "Class of 2016", Year = 2016 },
                new Community { Id = "c1", InstitutionId = Tenant, Name = "Accra Chapter" },
                new Community { Id = "c2", InstitutionId = Tenant, Name = "Kumasi Chapter" },
                Person("ama", 2015, complete: true), Person("ben", 2015, complete: true), Person("cy", 2015), Person("dee", 2015, "Pending"), Person("eli", 2016),
                Person("kojo", 2010), Person("esi", 2011), Person("yaa", 2012), Person("fay", 2013),
                new CommunityMembership { InstitutionId = Tenant, CommunityId = "c1", MemberId = "ben", Status = "Approved" },
                new CommunityMembership { InstitutionId = Tenant, CommunityId = "c1", MemberId = "cy", Status = "Approved" },
                new CommunityMembership { InstitutionId = Tenant, CommunityId = "c1", MemberId = "fay", Status = "Pending" },
                new CommunityMembership { InstitutionId = Tenant, CommunityId = "c2", MemberId = "eli", Status = "Approved" },
                new LeadershipRole { Id = "welcome", InstitutionId = Tenant, Name = "Welcomer", Privileges = [Members] },
                new LeadershipRole { Id = "voice", InstitutionId = Tenant, Name = "Chapter Voice", Privileges = [Message, Report] },
                new LeadershipRole { Id = "all", InstitutionId = Tenant, Name = "Patron", Privileges = [Members, Message, Events, Report] },
                new LeadershipRole { Id = "title", InstitutionId = Tenant, Name = "Honorary", Privileges = [] },
                new LeadershipRole { Id = "events", InstitutionId = Tenant, Name = "Events Lead", Privileges = [Events] },
                new LeadershipPost { InstitutionId = Tenant, MemberId = "ama", RoleId = "welcome", ScopeType = "Batch", ScopeId = "b2015" },
                new LeadershipPost { InstitutionId = Tenant, MemberId = "kojo", RoleId = "voice", ScopeType = "Community", ScopeId = "c1" },
                new LeadershipPost { InstitutionId = Tenant, MemberId = "esi", RoleId = "all", ScopeType = "Institution", ScopeId = "" },
                new LeadershipPost { InstitutionId = Tenant, MemberId = "yaa", RoleId = "title", ScopeType = "Batch", ScopeId = "b2015" },
                new LeadershipPost { InstitutionId = Tenant, MemberId = "eli", RoleId = "events", ScopeType = "Community", ScopeId = "c2" });
            await db.SaveChangesAsync();

            // A different institution, where "xama" is a patron of everything. None of it may reach institution 1.
            using var other = Db(Other);
            other.AddRange(
                new MemberEntity { Id = "xama", InstitutionId = Other, FirstName = "Ama", LastName = "Elsewhere", Email = "ama@y.test", Status = "Active" },
                new Batch { Id = "bx", InstitutionId = Other, Name = "Their class", Year = 2015 },
                new LeadershipRole { Id = "xall", InstitutionId = Other, Name = "Patron", Privileges = [Members, Message, Report] },
                new LeadershipPost { InstitutionId = Other, MemberId = "xama", RoleId = "xall", ScopeType = "Institution", ScopeId = "" });
            await other.SaveChangesAsync();
        }
    }

    private static async Task<Rig> Seeded(bool email = false, bool sms = false) { var r = new Rig(email, sms); await r.Seed(); return r; }

    // ── Which groups a member can manage ─────────────────────────────────────

    [Fact]
    public async Task A_leader_manages_only_the_group_their_role_was_given_for_with_only_what_it_allows()
    {
        var rig = await Seeded();

        var ama = (await rig.Service().GetGroupsAsync("ama")).Data!;
        var kojo = (await rig.Service().GetGroupsAsync("kojo")).Data!;

        var a = Assert.Single(ama);
        Assert.Equal(("Batch", "b2015", "Class of 2015"), (a.ScopeType, a.ScopeId, a.Name));
        Assert.Equal([Members], a.Privileges);
        Assert.Equal(["Welcomer"], a.RoleNames);
        var k = Assert.Single(kojo);
        Assert.Equal(("Community", "c1"), (k.ScopeType, k.ScopeId));
        Assert.Equal([Message, Report], k.Privileges);   // always in the order they are offered
    }

    [Fact]
    public async Task A_role_for_the_whole_institution_covers_every_group()
    {
        var rig = await Seeded();

        var esi = (await rig.Service().GetGroupsAsync("esi")).Data!;

        Assert.Equal(["Accra Chapter", "Class of 2015", "Class of 2016", "Kumasi Chapter"], esi.Select(g => g.Name).ToArray());
        Assert.All(esi, g => Assert.Equal([Members, Message, Events, Report], g.Privileges));
    }

    [Fact]
    public async Task A_title_that_allows_nothing_and_a_member_with_no_role_manage_nothing()
    {
        var rig = await Seeded();

        Assert.Empty((await rig.Service().GetGroupsAsync("yaa")).Data!);
        Assert.Empty((await rig.Service().GetGroupsAsync("ben")).Data!);
    }

    [Fact]
    public async Task Two_roles_in_the_same_group_add_up_without_listing_the_group_twice()
    {
        var rig = await Seeded();
        using (var db = rig.Db())
        {
            db.Add(new LeadershipPost { InstitutionId = Tenant, MemberId = "ama", RoleId = "voice", ScopeType = "Batch", ScopeId = "b2015" });
            await db.SaveChangesAsync();
        }

        var ama = (await rig.Service().GetGroupsAsync("ama")).Data!;

        var g = Assert.Single(ama);
        Assert.Equal([Members, Message, Report], g.Privileges);
        Assert.Equal(["Chapter Voice", "Welcomer"], g.RoleNames);
    }

    [Fact]
    public async Task Each_group_shows_how_many_people_are_waiting_only_where_the_leader_can_approve_them()
    {
        var rig = await Seeded();

        var ama = (await rig.Service().GetGroupsAsync("ama")).Data!.Single();
        var kojo = (await rig.Service().GetGroupsAsync("kojo")).Data!.Single();
        var esi = (await rig.Service().GetGroupsAsync("esi")).Data!.ToDictionary(g => g.Name, g => g.PendingCount);

        Assert.Equal(1, ama.PendingCount);     // dee has registered to 2015 and is waiting
        Assert.Equal(0, kojo.PendingCount);    // chapter c1 has fay waiting, but kojo's role does not include approving
        Assert.Equal((1, 1, 0, 0), (esi["Class of 2015"], esi["Accra Chapter"], esi["Class of 2016"], esi["Kumasi Chapter"]));
    }

    // ── Letting people in automatically ──────────────────────────────────────

    [Fact]
    public async Task A_leader_who_approves_members_can_switch_a_group_to_let_everyone_in_and_back()
    {
        var rig = await Seeded();
        Assert.False((await rig.Service().GetGroupsAsync("esi")).Data!.Single(g => g.ScopeId == "c1").AutoApproveMembers);

        Assert.Equal(200, (await rig.Service().SetAutoApproveAsync("esi", "Community", "c1", true)).Code);
        Assert.True((await rig.Service().GetGroupsAsync("esi")).Data!.Single(g => g.ScopeId == "c1").AutoApproveMembers);
        Assert.Contains(rig.Db().Set<InstitutionAuditLogEntry>(), e => e.Action == "Group Auto-Approval On");

        await rig.Service().SetAutoApproveAsync("esi", "Community", "c1", false);
        Assert.False((await rig.Service().GetGroupsAsync("esi")).Data!.Single(g => g.ScopeId == "c1").AutoApproveMembers);
    }

    [Fact]
    public async Task Only_a_leader_whose_role_includes_approving_members_may_change_it_or_even_see_it()
    {
        var rig = await Seeded();

        // kojo messages and reports for c1 but does not approve; eli leads c2 only; ama approves a year group.
        Assert.Equal(403, (await rig.Service().SetAutoApproveAsync("kojo", "Community", "c1", true)).Code);
        Assert.Equal(403, (await rig.Service().SetAutoApproveAsync("eli", "Community", "c1", true)).Code);
        Assert.False((await rig.Db().Set<Community>().FindAsync("c1"))!.AutoApproveMembers);
        Assert.Null((await rig.Service().GetGroupsAsync("kojo")).Data!.Single().AutoApproveMembers);
    }

    [Fact]
    public async Task A_year_group_has_no_such_switch_because_its_registrations_stay_with_the_administrators()
    {
        var rig = await Seeded();

        Assert.Equal(400, (await rig.Service().SetAutoApproveAsync("ama", "Batch", "b2015", true)).Code);
        Assert.Null((await rig.Service().GetGroupsAsync("ama")).Data!.Single().AutoApproveMembers);
    }

    // ── Report ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_report_counts_the_groups_members_and_nobody_elses()
    {
        var rig = await Seeded();

        var r = (await rig.Service().GetSummaryAsync("esi", "Batch", "b2015")).Data!;

        // ama, ben and cy are active in 2015; dee is waiting; eli is another year. ama and ben have full profiles, cy has none.
        Assert.Equal(("Class of 2015", 3, 1), (r.Name, r.TotalMembers, r.PendingRequests));
        Assert.Equal(67, r.ProfileCompletionPercent);
        Assert.Equal(2, r.ActiveLast30Days);
    }

    [Fact]
    public async Task A_communitys_report_counts_the_people_it_has_accepted_and_those_waiting()
    {
        var rig = await Seeded();

        var r = (await rig.Service().GetSummaryAsync("kojo", "Community", "c1")).Data!;

        Assert.Equal((2, 1), (r.TotalMembers, r.PendingRequests));   // ben and cy; fay is waiting
    }

    // ── Where the doors are locked ───────────────────────────────────────────

    [Theory]
    [InlineData("ama", "Batch", "b2016")]       // her own role, another year group
    [InlineData("ama", "Community", "c1")]      // her own role, a community
    [InlineData("ama", "Batch", "b2015")]       // right group, but her role does not include the report
    [InlineData("kojo", "Batch", "b2015")]
    [InlineData("kojo", "Community", "c2")]     // right kind of group, wrong one
    [InlineData("yaa", "Batch", "b2015")]       // a title with no privileges
    [InlineData("ben", "Community", "c1")]      // an ordinary member
    public async Task The_report_is_refused_without_the_privilege_in_that_exact_group(string who, string type, string id)
    {
        var rig = await Seeded();

        Assert.Equal(403, (await rig.Service().GetSummaryAsync(who, type, id)).Code);
    }

    [Theory]
    [InlineData("kojo", "Community", "c1")]     // she can message and report, but not manage members
    [InlineData("ama", "Batch", "b2016")]
    [InlineData("yaa", "Batch", "b2015")]
    [InlineData("ben", "Batch", "b2015")]
    public async Task The_member_list_is_refused_without_the_privilege_in_that_exact_group(string who, string type, string id)
    {
        var rig = await Seeded();

        Assert.Equal(403, (await rig.Service().GetMembersAsync(who, type, id)).Code);
    }

    [Fact]
    public async Task Only_year_groups_and_communities_can_be_acted_on_and_they_must_exist()
    {
        var rig = await Seeded();

        Assert.Equal(404, (await rig.Service().GetSummaryAsync("esi", "Institution", "")).Code);
        Assert.Equal(404, (await rig.Service().GetSummaryAsync("esi", "Batch", "missing")).Code);
        Assert.Equal(404, (await rig.Service().GetSummaryAsync("esi", "Planet", "b2015")).Code);
    }

    [Fact]
    public async Task A_patron_in_another_institution_gets_no_access_here_and_a_local_leader_gets_none_there()
    {
        var rig = await Seeded();

        // xama holds every privilege over institution 2, whose only group is "bx". Institution 1's groups do not exist as far as she is concerned.
        Assert.Equal(403, (await rig.Service().GetSummaryAsync("xama", "Batch", "b2015")).Code);
        Assert.Equal(403, (await rig.Service().GetMembersAsync("xama", "Community", "c1")).Code);
        Assert.Equal(403, (await rig.Service().MessageAsync("xama", "Batch", "b2015", "hello")).Code);
        Assert.Equal(403, (await rig.Service().DecideAsync("xama", "Community", "c1", "fay", true)).Code);
        Assert.Equal(0, await rig.Db().Notifications.CountAsync());
        Assert.Equal("Pending", (await rig.Db().CommunityMemberships.SingleAsync(m => m.MemberId == "fay")).Status);
        Assert.Equal(["Their class"], (await rig.Service(Other).GetGroupsAsync("xama")).Data!.Select(g => g.Name).ToArray());
        // And institution 1's leaders see none of institution 2's groups.
        Assert.Equal(404, (await rig.Service(Other).GetSummaryAsync("esi", "Batch", "b2015")).Code);
        Assert.DoesNotContain("Their class", (await rig.Service().GetGroupsAsync("esi")).Data!.Select(g => g.Name));
        Assert.Equal(404, (await rig.Service().GetSummaryAsync("xama", "Batch", "bx")).Code);
    }

    // ── Members ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_leader_sees_their_groups_people_with_those_waiting_first()
    {
        var rig = await Seeded();

        var list = (await rig.Service().GetMembersAsync("ama", "Batch", "b2015")).Data!;

        Assert.Equal(["dee"], list.Where(m => m.Status == "Pending").Select(m => m.MemberId));
        Assert.Equal("dee", list[0].MemberId);
        Assert.Equal(["ama", "ben", "cy", "dee"], list.Select(m => m.MemberId).Order().ToArray());
        Assert.Equal(["dee"], list.Where(m => m.CanDecide).Select(m => m.MemberId));   // a waiting registration can be approved
        Assert.All(list, m => Assert.False(m.CanDecline));                              // but only administrators decline one
    }

    [Fact]
    public async Task A_community_leader_can_decide_requests_waiting_for_their_community()
    {
        var rig = await Seeded();

        var list = (await rig.Service().GetMembersAsync("esi", "Community", "c1")).Data!;

        var waiting = Assert.Single(list, m => m.Status == "Pending");
        Assert.Equal(("fay", true, true), (waiting.MemberId, waiting.CanDecide, waiting.CanDecline));
        Assert.Equal(["ben", "cy", "fay"], list.Select(m => m.MemberId).Order().ToArray());
    }

    [Fact]
    public async Task Approving_a_request_lets_the_person_in_and_tells_them()
    {
        var rig = await Seeded();

        var result = await rig.Service().DecideAsync("esi", "Community", "c1", "fay", true);

        Assert.Equal(200, result.Code);
        var m = await rig.Db().CommunityMemberships.SingleAsync(x => x.MemberId == "fay");
        Assert.Equal(("Approved", "esi"), (m.Status, m.DecidedBy));
        var n = await rig.Db().Notifications.SingleAsync(x => x.RecipientId == "fay");
        Assert.Contains("approved", n.Body);
    }

    [Fact]
    public async Task Declining_a_request_keeps_the_person_out_and_tells_them()
    {
        var rig = await Seeded();

        Assert.Equal(200, (await rig.Service().DecideAsync("esi", "Community", "c1", "fay", false)).Code);

        Assert.Equal("Rejected", (await rig.Db().CommunityMemberships.SingleAsync(x => x.MemberId == "fay")).Status);
        Assert.Contains("declined", (await rig.Db().Notifications.SingleAsync(x => x.RecipientId == "fay")).Body);
    }

    [Theory]
    [InlineData("kojo", "Community", "c1", "fay")]   // reports and messages, but may not decide
    [InlineData("ama", "Community", "c1", "fay")]    // may manage members, but of another group
    [InlineData("ben", "Community", "c1", "fay")]    // an ordinary member
    public async Task A_decision_without_the_privilege_in_that_group_changes_nothing(string who, string type, string id, string target)
    {
        var rig = await Seeded();

        Assert.Equal(403, (await rig.Service().DecideAsync(who, type, id, target, true)).Code);

        Assert.Equal("Pending", (await rig.Db().CommunityMemberships.SingleAsync(x => x.MemberId == "fay")).Status);
        Assert.Equal(0, await rig.Db().Notifications.CountAsync());
    }

    [Fact]
    public async Task Only_a_waiting_request_can_be_decided()
    {
        var rig = await Seeded();

        Assert.Equal(404, (await rig.Service().DecideAsync("esi", "Community", "c1", "ben", true)).Code);      // already in
        Assert.Equal(404, (await rig.Service().DecideAsync("esi", "Community", "c1", "nobody", true)).Code);
        Assert.Equal(404, (await rig.Service().DecideAsync("ama", "Batch", "b2015", "ben", true)).Code);       // already active
        Assert.Equal(404, (await rig.Service().DecideAsync("ama", "Batch", "b2015", "eli", true)).Code);       // another year group's member
    }

    // ── Approving a year group's registrations ───────────────────────────────

    [Fact]
    public async Task A_year_group_leader_approves_a_registration_the_way_an_administrator_would()
    {
        var rig = await Seeded();

        var result = await rig.Service().DecideAsync("ama", "Batch", "b2015", "dee", true);

        Assert.Equal(200, result.Code);
        var dee = await rig.Db().Members.SingleAsync(m => m.Id == "dee");
        Assert.Equal(("Active", "ama"), (dee.Status, dee.UpdatedBy));
        Assert.StartsWith("UMAT-2015-", dee.MemberNumber);               // the same numbering an administrator's approval gives
        Assert.Equal(1, rig.Queued("MemberStatusChanged"));             // the welcome notification
        Assert.Equal(1, rig.Queued("Email"));                           // and the welcome email
        var audit = await rig.Db().Set<InstitutionAuditLogEntry>().SingleAsync();
        Assert.Equal(("Member Approved", "ama Test (leader)"), (audit.Action, audit.Actor));
        Assert.Contains("dee@x.test", audit.Target);
    }

    [Fact]
    public async Task Approving_twice_gives_one_member_number_and_one_welcome()
    {
        var rig = await Seeded();
        await rig.Service().DecideAsync("ama", "Batch", "b2015", "dee", true);

        Assert.Equal(404, (await rig.Service().DecideAsync("ama", "Batch", "b2015", "dee", true)).Code);
        Assert.Equal(1, rig.Queued("MemberStatusChanged"));
    }

    [Fact]
    public async Task A_year_group_registration_cannot_be_declined_by_a_leader()
    {
        var rig = await Seeded();

        var result = await rig.Service().DecideAsync("ama", "Batch", "b2015", "dee", false);

        Assert.Equal(400, result.Code);
        Assert.Contains("administrators", result.Message);
        Assert.Equal("Pending", await rig.Db().MemberStatus("dee"));
        Assert.Equal(0, rig.Queued("MemberStatusChanged"));
    }

    [Theory]
    [InlineData("kojo")]      // may message and report, not manage members
    [InlineData("yaa")]       // a title with no privileges
    [InlineData("ben")]       // an ordinary member of the group
    [InlineData("eli")]       // manages events elsewhere, not members
    public async Task A_registration_cannot_be_approved_without_the_privilege_in_that_group(string who)
    {
        var rig = await Seeded();

        Assert.Equal(403, (await rig.Service().DecideAsync(who, "Batch", "b2015", "dee", true)).Code);
        Assert.Equal("Pending", await rig.Db().MemberStatus("dee"));
        Assert.Null((await rig.Db().Members.SingleAsync(m => m.Id == "dee")).MemberNumber);
    }

    [Fact]
    public async Task A_leader_of_one_year_group_cannot_approve_in_another_even_for_a_pending_member_of_their_own_year()
    {
        var rig = await Seeded();
        using (var db = rig.Db()) { db.Add(new MemberEntity { Id = "gus", InstitutionId = Tenant, FirstName = "gus", LastName = "T", Email = "g@x.test", Status = "Pending", GraduationYear = 2016 }); await db.SaveChangesAsync(); }

        Assert.Equal(403, (await rig.Service().DecideAsync("ama", "Batch", "b2016", "gus", true)).Code);   // not her group
        Assert.Equal(404, (await rig.Service().DecideAsync("ama", "Batch", "b2015", "gus", true)).Code);   // her group, but he is of 2016
        Assert.Equal("Pending", await rig.Db().MemberStatus("gus"));
    }

    // ── Message ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_message_reaches_everyone_active_in_the_group_except_the_sender_and_leaves_a_receipt()
    {
        var rig = await Seeded();

        var result = await rig.Service().MessageAsync("kojo", "Community", "c1", "  Our meeting   is on   Saturday. ");

        Assert.Equal(200, result.Code);
        Assert.Equal(2, result.Data!.Recipients);   // ben and cy; fay is only waiting, kojo is the sender
        var sent = await rig.Db().Notifications.Where(n => n.Type == ManageService.MessageType).ToListAsync();
        Assert.Equal(["ben", "cy"], sent.Select(n => n.RecipientId).Order().ToArray());
        Assert.All(sent, n => { Assert.Equal("Accra Chapter", n.Title); Assert.Equal("kojo Test: Our meeting is on Saturday.", n.Body); Assert.Equal("kojo", n.CreatedBy); });
        var receipt = await rig.Db().Notifications.SingleAsync(n => n.Type == ManageService.MessageSentType);
        Assert.Equal("kojo", receipt.RecipientId);
    }

    [Fact]
    public async Task A_year_group_message_goes_to_that_years_active_members_only()
    {
        var rig = await Seeded();

        var result = await rig.Service().MessageAsync("esi", "Batch", "b2015", "Welcome");

        Assert.Equal(3, result.Data!.Recipients);   // ama, ben, cy: not dee (waiting), not eli (2016)
        Assert.Equal(["ama", "ben", "cy"], (await rig.Db().Notifications.Where(n => n.Type == ManageService.MessageType).Select(n => n.RecipientId).ToListAsync()).Order().ToArray());
    }

    [Theory]
    [InlineData("ama", "Batch", "b2015")]       // may manage members but not message
    [InlineData("kojo", "Community", "c2")]     // may message, but another community
    [InlineData("yaa", "Batch", "b2015")]
    [InlineData("ben", "Community", "c1")]
    public async Task A_message_without_the_privilege_in_that_group_is_refused_and_nothing_is_sent(string who, string type, string id)
    {
        var rig = await Seeded();

        Assert.Equal(403, (await rig.Service().MessageAsync(who, type, id, "hello")).Code);
        Assert.Equal(0, await rig.Db().Notifications.CountAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public async Task An_empty_message_is_refused(string text)
    {
        var rig = await Seeded();

        Assert.Equal(400, (await rig.Service().MessageAsync("kojo", "Community", "c1", text)).Code);
        Assert.Equal(0, await rig.Db().Notifications.CountAsync());
    }

    [Fact]
    public async Task A_message_over_500_characters_is_refused()
    {
        var rig = await Seeded();

        Assert.Equal(400, (await rig.Service().MessageAsync("kojo", "Community", "c1", new string('a', 501))).Code);
        Assert.Equal(200, (await rig.Service().MessageAsync("kojo", "Community", "c1", new string('a', 500))).Code);
    }

    [Fact]
    public async Task A_leader_can_send_three_messages_a_day_to_one_group_and_the_fourth_waits_until_tomorrow()
    {
        var rig = await Seeded();

        for (var i = 0; i < 3; i++) Assert.Equal(200, (await rig.Service().MessageAsync("kojo", "Community", "c1", $"note {i}")).Code);
        var fourth = await rig.Service().MessageAsync("kojo", "Community", "c1", "one more");

        Assert.Equal(400, fourth.Code);
        Assert.Contains("3 messages a day", fourth.Message);
        Assert.Equal(6, await rig.Db().Notifications.CountAsync(n => n.Type == ManageService.MessageType));   // two people, three messages: the fourth sent nothing
        // The limit is per group and per leader: an institution-wide patron is not held to kojo's count.
        Assert.Equal(200, (await rig.Service().MessageAsync("esi", "Community", "c1", "hello from me")).Code);
    }

    [Theory]
    [InlineData(1, "1 message a day")]
    [InlineData(5, "5 messages a day")]
    public async Task The_institution_chooses_how_many_messages_a_leader_may_send_a_day(int perDay, string wording)
    {
        var rig = await Seeded();
        using (var db = rig.Db()) { (await db.Institutions.SingleAsync(i => i.Id == Tenant)).GroupMessagesPerDay = perDay; await db.SaveChangesAsync(); }

        for (var i = 0; i < perDay; i++) Assert.Equal(200, (await rig.Service().MessageAsync("kojo", "Community", "c1", $"note {i}")).Code);
        var over = await rig.Service().MessageAsync("kojo", "Community", "c1", "one too many");

        Assert.Equal(400, over.Code);
        Assert.Contains(wording, over.Message);
        var channels = (await rig.Service().GetChannelsAsync("kojo", "Community", "c1")).Data!;
        Assert.Equal((perDay, 0), (channels.PerDay, channels.LeftToday));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public async Task A_missing_or_nonsense_limit_falls_back_to_three(int stored)
    {
        var rig = await Seeded();
        using (var db = rig.Db()) { (await db.Institutions.SingleAsync(i => i.Id == Tenant)).GroupMessagesPerDay = stored; await db.SaveChangesAsync(); }

        Assert.Equal(3, (await rig.Service().GetChannelsAsync("kojo", "Community", "c1")).Data!.PerDay);
    }

    [Fact]
    public async Task A_message_to_a_big_group_reaches_everyone_in_one_alert_however_many_there_are()
    {
        var rig = await Seeded();
        using (var db = rig.Db())
        {
            db.Add(new Community { Id = "big", InstitutionId = Tenant, Name = "Everyone" });
            db.Add(new LeadershipPost { InstitutionId = Tenant, MemberId = "kojo", RoleId = "voice", ScopeType = "Community", ScopeId = "big" });
            for (var i = 0; i < 1250; i++)
            {
                db.Add(new MemberEntity { Id = $"bulk{i}", InstitutionId = Tenant, FirstName = "B", LastName = $"{i}", Email = $"b{i}@x.test", Status = "Active" });
                db.Add(new CommunityMembership { Id = $"bm{i}", InstitutionId = Tenant, CommunityId = "big", MemberId = $"bulk{i}", Status = "Approved" });
            }
            await db.SaveChangesAsync();
        }

        var result = await rig.Service().MessageAsync("kojo", "Community", "big", "Hello everyone");

        Assert.Equal((200, 1250), (result.Code, result.Data!.Recipients));
        Assert.Equal(1250, await rig.Db().Notifications.CountAsync(n => n.Type == ManageService.MessageType && n.RelatedEntityId == "big"));   // saved in chunks, none lost
        Assert.Equal(1, rig.Queued("Broadcast"));
    }

    [Fact]
    public async Task The_member_count_covers_everyone_in_the_group_and_those_waiting_and_is_for_those_who_may_approve()
    {
        var rig = await Seeded();

        var counts = (await rig.Service().GetPeopleCountsAsync("ama", "Batch", "b2015")).Data!;

        Assert.Equal((3, 1), (counts.Members, counts.Waiting));   // ama, ben and cy are in; dee is waiting
        Assert.Equal(403, (await rig.Service().GetPeopleCountsAsync("kojo", "Batch", "b2015")).Code);   // kojo messages and reports for a chapter, but does not approve here
    }

    // ── What has already been sent ───────────────────────────────────────────

    [Fact]
    public async Task Leaders_who_may_message_a_group_see_what_every_leader_sent_it_newest_first()
    {
        var rig = await Seeded();
        await rig.Service().MessageAsync("kojo", "Community", "c1", "Reunion: Saturday at ten");
        await Task.Delay(15);
        await rig.Service().MessageAsync("esi", "Community", "c1", "Bring a friend");

        var history = (await rig.Service().GetMessagesAsync("kojo", "Community", "c1")).Data!;

        Assert.Equal(["Bring a friend", "Reunion: Saturday at ten"], history.Select(h => h.Text).ToArray());   // the colon inside a message survives
        Assert.Equal([false, true], history.Select(h => h.IsMine).ToArray());
        Assert.All(history, h => Assert.True(h.Recipients > 0));
    }

    [Fact]
    public async Task Message_history_is_for_the_group_asked_about_and_for_those_who_may_message_it()
    {
        var rig = await Seeded();
        await rig.Service().MessageAsync("kojo", "Community", "c1", "Only for Accra");

        Assert.Empty((await rig.Service().GetMessagesAsync("esi", "Community", "c2")).Data!);
        Assert.Equal(403, (await rig.Service().GetMessagesAsync("ama", "Community", "c1")).Code);   // ama welcomes Class of 2015 only
    }

    // ── Reaching people beyond the notification ──────────────────────────────

    [Fact]
    public async Task A_message_always_also_goes_out_as_a_phone_alert()
    {
        var rig = await Seeded();

        await rig.Service().MessageAsync("kojo", "Community", "c1", "Saturday at ten");

        Assert.Equal(1, rig.Queued("Broadcast"));
    }

    [Fact]
    public async Task A_message_to_a_group_nobody_else_is_in_queues_nothing()
    {
        var rig = await Seeded();
        using (var db = rig.Db()) { db.Add(new LeadershipPost { InstitutionId = Tenant, MemberId = "kojo", RoleId = "voice", ScopeType = "Community", ScopeId = "empty" }); db.Add(new Community { Id = "empty", InstitutionId = Tenant, Name = "Empty" }); await db.SaveChangesAsync(); }

        var result = await rig.Service().MessageAsync("kojo", "Community", "empty", "anyone?");

        Assert.Equal(0, result.Data!.Recipients);
        Assert.Equal(0, rig.Queued("Broadcast"));
    }

    [Theory]
    [InlineData("Email")]
    [InlineData("Sms")]
    [InlineData("Email", "Sms")]
    public async Task Email_and_text_are_refused_where_the_platform_has_not_switched_them_on_and_nothing_is_sent(params string[] channels)
    {
        var rig = await Seeded(email: false, sms: false);

        var result = await rig.Service().MessageAsync("kojo", "Community", "c1", "hello", channels);

        Assert.Equal(400, result.Code);
        Assert.Contains("not switched on", result.Message);
        Assert.Equal(0, await rig.Db().Notifications.CountAsync());
        Assert.Equal(0, rig.Queued("Broadcast"));
    }

    [Fact]
    public async Task Only_the_extra_channels_the_platform_allows_can_be_used()
    {
        var rig = await Seeded(email: true, sms: false);

        Assert.Equal(200, (await rig.Service().MessageAsync("kojo", "Community", "c1", "by email", ["Email"])).Code);
        Assert.Equal(400, (await rig.Service().MessageAsync("kojo", "Community", "c1", "by text", ["Sms"])).Code);
        Assert.Equal(400, (await rig.Service().MessageAsync("kojo", "Community", "c1", "both", ["email", "sms"])).Code);   // case does not slip it through
        Assert.Equal(1, rig.Queued("Broadcast"));
    }

    [Theory]
    [InlineData("Carrier pigeon")]
    [InlineData("InApp")]
    [InlineData("Push")]
    public async Task A_channel_that_is_not_email_or_text_is_refused(string channel)
    {
        var rig = await Seeded(email: true, sms: true);

        Assert.Equal(400, (await rig.Service().MessageAsync("kojo", "Community", "c1", "hello", [channel])).Code);
        Assert.Equal(0, await rig.Db().Notifications.CountAsync());
    }

    [Fact]
    public async Task The_options_shown_to_a_leader_follow_what_the_platform_has_switched_on()
    {
        var off = await Seeded();
        var on = await Seeded(email: true, sms: true);

        Assert.Equal((false, false), ((await off.Service().GetChannelsAsync("kojo", "Community", "c1")).Data!.Email, (await off.Service().GetChannelsAsync("kojo", "Community", "c1")).Data!.Sms));
        var all = (await on.Service().GetChannelsAsync("kojo", "Community", "c1")).Data!;
        Assert.Equal((true, true), (all.Email, all.Sms));
        Assert.Equal(403, (await on.Service().GetChannelsAsync("ama", "Batch", "b2015")).Code);   // may not message
    }

    [Fact]
    public async Task Before_sending_a_leader_is_told_how_many_will_receive_it_and_how_many_messages_are_left_today()
    {
        var rig = await Seeded();

        var before = (await rig.Service().GetChannelsAsync("kojo", "Community", "c1")).Data!;
        await rig.Service().MessageAsync("kojo", "Community", "c1", "one");
        await rig.Service().MessageAsync("kojo", "Community", "c1", "two");
        var after = (await rig.Service().GetChannelsAsync("kojo", "Community", "c1")).Data!;

        Assert.Equal((2, 3), (before.Recipients, before.LeftToday));   // ben and cy; fay is only waiting and kojo is the sender
        Assert.Equal((2, 1), (after.Recipients, after.LeftToday));
        await rig.Service().MessageAsync("kojo", "Community", "c1", "three");
        Assert.Equal(0, (await rig.Service().GetChannelsAsync("kojo", "Community", "c1")).Data!.LeftToday);
    }

    [Fact]
    public async Task The_count_is_for_that_leader_and_that_group_only()
    {
        var rig = await Seeded();
        await rig.Service().MessageAsync("esi", "Community", "c1", "from esi");

        Assert.Equal(3, (await rig.Service().GetChannelsAsync("kojo", "Community", "c1")).Data!.LeftToday);    // esi's message is not kojo's
        Assert.Equal(3, (await rig.Service().GetChannelsAsync("esi", "Batch", "b2015")).Data!.LeftToday);      // and another group has its own allowance
        Assert.Equal(2, (await rig.Service().GetChannelsAsync("esi", "Community", "c1")).Data!.LeftToday);
        Assert.Equal(3, (await rig.Service().GetChannelsAsync("esi", "Batch", "b2015")).Data!.Recipients);     // ama, ben, cy: dee is waiting, eli is another year
    }

    [Fact]
    public async Task A_message_is_recorded_in_the_audit_log_under_the_leaders_name()
    {
        var rig = await Seeded(email: true);

        await rig.Service().MessageAsync("kojo", "Community", "c1", "hello", ["Email"]);

        var audit = await rig.Db().Set<InstitutionAuditLogEntry>().SingleAsync();
        Assert.Equal(("kojo Test (leader)", "Group Message Sent"), (audit.Actor, audit.Action));
        Assert.Contains("Accra Chapter", audit.Target);
        Assert.Contains("2 people", audit.Target);
        Assert.Contains("Email", audit.Target);
    }

    // ── Events ───────────────────────────────────────────────────────────────

    private static SaveGroupEventRequest Event(string title = "Reunion lunch", DateTime? start = null, DateTime? end = null, int? capacity = null, string venue = "Main Hall")
        => new() { Title = title, Venue = venue, Description = "Bring a friend.", StartDate = start ?? DateTime.UtcNow.AddDays(14), EndDate = end, Capacity = capacity };

    [Fact]
    public async Task A_year_group_leader_creates_a_free_event_for_that_year_group_only()
    {
        var rig = await Seeded();
        using (var db = rig.Db()) { db.Add(new LeadershipPost { InstitutionId = Tenant, MemberId = "ama", RoleId = "events", ScopeType = "Batch", ScopeId = "b2015" }); await db.SaveChangesAsync(); }

        var result = await rig.Service().CreateEventAsync("ama", "Batch", "b2015", Event());

        Assert.Equal(201, result.Code);
        var ev = await rig.Db().Events.SingleAsync();
        Assert.Equal(("Reunion lunch", "Main Hall", "ama", "Upcoming"), (ev.Title, ev.Venue, ev.CreatedBy, ev.Status));
        Assert.Equal([2015], ev.YearGroups);
        Assert.Null(ev.CommunityId);
        Assert.False(ev.IsPublic);
        Assert.False(ev.IsTicketed);
        Assert.Null(ev.TicketPrice);
        Assert.Contains("Bring a friend", ev.Description);
        Assert.Equal(1, rig.Queued("EventReminder"));
        Assert.Equal("Event Created", (await rig.Db().Set<InstitutionAuditLogEntry>().SingleAsync()).Action);
    }

    [Fact]
    public async Task A_community_leaders_event_belongs_to_that_community()
    {
        var rig = await Seeded();

        var result = await rig.Service().CreateEventAsync("eli", "Community", "c2", Event());

        Assert.Equal(201, result.Code);
        var ev = await rig.Db().Events.SingleAsync();
        Assert.Equal("c2", ev.CommunityId);
        Assert.Null(ev.YearGroups);
    }

    [Theory]
    [InlineData("kojo", "Community", "c1")]    // messages and reports, not events
    [InlineData("ama", "Batch", "b2015")]      // manages members, not events
    [InlineData("yaa", "Batch", "b2015")]
    [InlineData("ben", "Community", "c1")]
    [InlineData("eli", "Community", "c1")]     // events lead, but of another community
    [InlineData("eli", "Batch", "b2016")]
    public async Task An_event_cannot_be_made_without_the_privilege_in_that_group(string who, string type, string id)
    {
        var rig = await Seeded();

        Assert.Equal(403, (await rig.Service().CreateEventAsync(who, type, id, Event())).Code);
        Assert.Equal(403, (await rig.Service().GetEventsAsync(who, type, id)).Code);
        Assert.Equal(0, await rig.Db().Events.CountAsync());
    }

    [Fact]
    public async Task A_patron_of_the_whole_institution_can_make_events_in_every_group()
    {
        var rig = await Seeded();

        Assert.Equal(201, (await rig.Service().CreateEventAsync("esi", "Batch", "b2016", Event("A"))).Code);
        Assert.Equal(201, (await rig.Service().CreateEventAsync("esi", "Community", "c1", Event("B"))).Code);
        Assert.Equal(2, await rig.Db().Events.CountAsync());
    }

    [Fact]
    public async Task A_bad_event_is_refused_and_nothing_is_saved()
    {
        var rig = await Seeded();
        var now = DateTime.UtcNow;

        Assert.Equal(400, (await rig.Service().CreateEventAsync("esi", "Batch", "b2015", Event(title: "  "))).Code);
        Assert.Equal(400, (await rig.Service().CreateEventAsync("esi", "Batch", "b2015", Event(title: new string('a', 151)))).Code);
        Assert.Equal(400, (await rig.Service().CreateEventAsync("esi", "Batch", "b2015", Event(venue: ""))).Code);
        Assert.Equal(400, (await rig.Service().CreateEventAsync("esi", "Batch", "b2015", Event(start: now.AddDays(-1)))).Code);                       // already past
        Assert.Equal(400, (await rig.Service().CreateEventAsync("esi", "Batch", "b2015", Event(start: now.AddDays(5), end: now.AddDays(4)))).Code);   // ends before it starts
        Assert.Equal(400, (await rig.Service().CreateEventAsync("esi", "Batch", "b2015", Event(capacity: 0))).Code);
        Assert.Equal(0, await rig.Db().Events.CountAsync());
    }

    [Fact]
    public async Task Only_the_leaders_own_events_can_be_edited_and_everyone_in_the_group_can_see_them()
    {
        var rig = await Seeded();
        await rig.Service().CreateEventAsync("esi", "Community", "c2", Event("Patron's event"));
        await rig.Service().CreateEventAsync("eli", "Community", "c2", Event("Eli's event"));

        var seenByEli = (await rig.Service().GetEventsAsync("eli", "Community", "c2")).Data!;

        Assert.Equal(["Eli's event", "Patron's event"], seenByEli.Select(e => e.Title).Order().ToArray());
        Assert.Equal(["Eli's event"], seenByEli.Where(e => e.CanEdit).Select(e => e.Title).ToArray());
        var patrons = seenByEli.Single(e => e.Title == "Patron's event");
        Assert.Equal(404, (await rig.Service().UpdateEventAsync("eli", "Community", "c2", patrons.Id, Event("Hijacked"))).Code);
        Assert.Equal(404, (await rig.Service().CancelEventAsync("eli", "Community", "c2", patrons.Id)).Code);
        Assert.Equal("Patron's event", (await rig.Db().Events.SingleAsync(e => e.Id == patrons.Id)).Title);
    }

    [Fact]
    public async Task An_event_can_only_be_changed_through_the_group_it_belongs_to()
    {
        var rig = await Seeded();
        var made = (await rig.Service().CreateEventAsync("esi", "Community", "c1", Event("Chapter night"))).Data!;

        // esi may manage events everywhere, but this event is c1's: asking for it through another group finds nothing.
        Assert.Equal(404, (await rig.Service().UpdateEventAsync("esi", "Community", "c2", made.Id, Event("Moved"))).Code);
        Assert.Equal(404, (await rig.Service().UpdateEventAsync("esi", "Batch", "b2015", made.Id, Event("Moved"))).Code);
        Assert.Equal(200, (await rig.Service().UpdateEventAsync("esi", "Community", "c1", made.Id, Event("Chapter night, now at six", venue: "Annex"))).Code);
        Assert.Equal(("Chapter night, now at six", "Annex"), ((await rig.Db().Events.SingleAsync()).Title, (await rig.Db().Events.SingleAsync()).Venue));
    }

    [Fact]
    public async Task A_ticketed_event_is_not_the_leaders_to_change()
    {
        var rig = await Seeded();
        using (var db = rig.Db()) { db.Add(new AlumniEvent { Id = "paid", InstitutionId = Tenant, Title = "Gala", Venue = "Hotel", StartDate = DateTime.UtcNow.AddDays(9), CommunityId = "c2", CreatedBy = "eli", IsTicketed = true, TicketPrice = 50 }); await db.SaveChangesAsync(); }

        Assert.Equal(404, (await rig.Service().UpdateEventAsync("eli", "Community", "c2", "paid", Event("Free gala"))).Code);
        Assert.Equal(404, (await rig.Service().CancelEventAsync("eli", "Community", "c2", "paid")).Code);
        Assert.False((await rig.Service().GetEventsAsync("eli", "Community", "c2")).Data!.Single().CanEdit);
        Assert.True((await rig.Db().Events.SingleAsync()).IsTicketed);
    }

    [Fact]
    public async Task Changing_the_details_tells_the_people_who_are_going_but_a_new_title_alone_does_not()
    {
        var rig = await Seeded();
        var id = (await rig.Service().CreateEventAsync("eli", "Community", "c2", Event(start: DateTime.UtcNow.AddDays(10)))).Data!.Id;
        using (var db = rig.Db()) { db.AddRange(new EventRsvp { InstitutionId = Tenant, EventId = id, MemberId = "ben", Status = EventRsvpStatuses.Confirmed }, new EventRsvp { InstitutionId = Tenant, EventId = id, MemberId = "cy", Status = EventRsvpStatuses.Confirmed }); await db.SaveChangesAsync(); }
        var ev = await rig.Db().Events.SingleAsync();

        await rig.Service().UpdateEventAsync("eli", "Community", "c2", id, Event("A better title", start: ev.StartDate, end: ev.EndDate, venue: ev.Venue));
        Assert.Equal(0, rig.Queued("EventDetailsChanged"));

        await rig.Service().UpdateEventAsync("eli", "Community", "c2", id, Event("A better title", start: ev.StartDate.AddHours(2), venue: "New venue"));
        Assert.Equal(2, rig.Queued("EventDetailsChanged"));
    }

    [Fact]
    public async Task Cancelling_an_event_tells_those_going_once_and_it_cannot_be_cancelled_twice_or_edited_after()
    {
        var rig = await Seeded();
        var id = (await rig.Service().CreateEventAsync("eli", "Community", "c2", Event())).Data!.Id;
        using (var db = rig.Db()) { db.Add(new EventRsvp { InstitutionId = Tenant, EventId = id, MemberId = "ben", Status = EventRsvpStatuses.Confirmed }); await db.SaveChangesAsync(); }

        Assert.Equal(200, (await rig.Service().CancelEventAsync("eli", "Community", "c2", id)).Code);

        Assert.Equal("Cancelled", (await rig.Db().Events.SingleAsync()).Status);
        Assert.Equal(1, rig.Queued("EventCancelled"));
        Assert.Equal(400, (await rig.Service().CancelEventAsync("eli", "Community", "c2", id)).Code);
        Assert.Equal(400, (await rig.Service().UpdateEventAsync("eli", "Community", "c2", id, Event())).Code);
        Assert.Equal(1, rig.Queued("EventCancelled"));
        Assert.False((await rig.Service().GetEventsAsync("eli", "Community", "c2")).Data!.Single().CanEdit);
    }

    [Fact]
    public async Task A_year_groups_events_include_those_aimed_at_it_but_not_a_communitys()
    {
        var rig = await Seeded();
        using (var db = rig.Db())
        {
            db.AddRange(
                new AlumniEvent { Id = "e15", InstitutionId = Tenant, Title = "2015 only", Venue = "v", StartDate = DateTime.UtcNow.AddDays(3), YearGroups = [2015] },
                new AlumniEvent { Id = "e1516", InstitutionId = Tenant, Title = "2015 and 2016", Venue = "v", StartDate = DateTime.UtcNow.AddDays(4), YearGroups = [2015, 2016] },
                new AlumniEvent { Id = "e16", InstitutionId = Tenant, Title = "2016 only", Venue = "v", StartDate = DateTime.UtcNow.AddDays(5), YearGroups = [2016] },
                new AlumniEvent { Id = "ec", InstitutionId = Tenant, Title = "Chapter", Venue = "v", StartDate = DateTime.UtcNow.AddDays(6), CommunityId = "c1", YearGroups = [2015] },
                new AlumniEvent { Id = "eall", InstitutionId = Tenant, Title = "Everyone", Venue = "v", StartDate = DateTime.UtcNow.AddDays(7) });
            await db.SaveChangesAsync();
        }

        var titles = (await rig.Service().GetEventsAsync("esi", "Batch", "b2015")).Data!.Select(e => e.Title).Order().ToArray();

        Assert.Equal(["2015 and 2016", "2015 only"], titles);
    }

    [Fact]
    public async Task A_patron_in_another_institution_cannot_make_or_see_events_here()
    {
        var rig = await Seeded();

        Assert.Equal(403, (await rig.Service().CreateEventAsync("xama", "Batch", "b2015", Event())).Code);
        Assert.Equal(403, (await rig.Service().GetEventsAsync("xama", "Community", "c1")).Code);
        Assert.Equal(0, await rig.Db().Events.CountAsync());
    }

    // ── Pictures and links on events ─────────────────────────────────────────

    private static SaveGroupEventRequest WithMedia(Action<SaveGroupEventRequest> set) { var r = Event(); set(r); return r; }

    [Fact]
    public async Task An_event_can_carry_a_banner_photos_a_map_link_and_videos_all_stored_by_the_platform()
    {
        var rig = await Seeded();

        var result = await rig.Service().CreateEventAsync("eli", "Community", "c2", WithMedia(r =>
        {
            r.BannerImage = Files.Jpeg("banner.jpg"); r.Images = [Files.Png("1.png"), Files.Webp("2.webp")];
            r.GoogleLocationUrl = "https://maps.google.com/?q=hall"; r.YoutubeVideoUrls = ["https://youtu.be/abc", "https://www.youtube.com/watch?v=def"];
        }));

        Assert.Equal(201, result.Code);
        var ev = await rig.Db().Events.SingleAsync();
        Assert.StartsWith("https://cdn.test/", ev.BannerImageUrl);
        Assert.EndsWith(".jpg", ev.BannerImageUrl);
        Assert.Equal(2, ev.ImageUrls!.Count);
        Assert.All(ev.ImageUrls, u => Assert.StartsWith("https://cdn.test/", u));
        Assert.Equal(("https://maps.google.com/?q=hall", 2), (ev.GoogleLocationUrl, ev.YoutubeVideoUrls!.Count));
        Assert.Equal(3, rig.Storage.Uploaded.Count);
        Assert.Equal(ev.BannerImageUrl, result.Data!.BannerImageUrl);   // what the editor needs to show them again
    }

    [Fact]
    public async Task An_event_without_pictures_or_links_has_none()
    {
        var rig = await Seeded();

        await rig.Service().CreateEventAsync("eli", "Community", "c2", Event());

        var ev = await rig.Db().Events.SingleAsync();
        Assert.Null(ev.BannerImageUrl); Assert.Null(ev.ImageUrls); Assert.Null(ev.YoutubeVideoUrls); Assert.Null(ev.GoogleLocationUrl);
        Assert.Empty(rig.Storage.Uploaded);
    }

    [Fact]
    public async Task A_bad_picture_refuses_the_whole_event_and_nothing_is_stored_or_uploaded()
    {
        var rig = await Seeded();

        var disguised = await rig.Service().CreateEventAsync("eli", "Community", "c2", WithMedia(r => { r.BannerImage = Files.Png(); r.Images = [Files.Png("ok.png"), Files.Disguised("bad.png")]; }));
        var tooMany = await rig.Service().CreateEventAsync("eli", "Community", "c2", WithMedia(r => r.Images = Enumerable.Range(0, 9).Select(i => Files.Png($"{i}.png")).ToList()));

        Assert.Equal((400, 400), (disguised.Code, tooMany.Code));
        Assert.Equal(0, await rig.Db().Events.CountAsync());
        Assert.Empty(rig.Storage.Uploaded);
    }

    [Theory]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("not a link", null)]
    [InlineData(null, "https://vimeo.com/123")]
    [InlineData(null, "http://www.youtube.com/watch?v=abc")]
    public async Task A_bad_map_link_or_video_refuses_the_event(string? map, string? video)
    {
        var rig = await Seeded();

        var result = await rig.Service().CreateEventAsync("eli", "Community", "c2", WithMedia(r => { r.GoogleLocationUrl = map; r.YoutubeVideoUrls = video is null ? null : [video]; }));

        Assert.Equal(400, result.Code);
        Assert.Equal(0, await rig.Db().Events.CountAsync());
    }

    [Fact]
    public async Task Editing_keeps_what_was_asked_for_ignores_pictures_it_never_had_and_can_replace_or_remove_the_banner()
    {
        var rig = await Seeded();
        var made = (await rig.Service().CreateEventAsync("eli", "Community", "c2", WithMedia(r => { r.BannerImage = Files.Png(); r.Images = [Files.Png("a.png"), Files.Png("b.png")]; }))).Data!;
        var first = made.ImageUrls![0];

        var edited = await rig.Service().UpdateEventAsync("eli", "Community", "c2", made.Id, WithMedia(r =>
        {
            r.ExistingImageUrls = [first, "https://evil.example/tracker.png"]; r.Images = [Files.Jpeg("c.jpg")]; r.RemoveBanner = true;
        }));

        Assert.Equal(200, edited.Code);
        var ev = await rig.Db().Events.SingleAsync();
        Assert.Equal(2, ev.ImageUrls!.Count);
        Assert.Equal(first, ev.ImageUrls[0]);                 // the one kept
        Assert.DoesNotContain("https://evil.example/tracker.png", ev.ImageUrls);
        Assert.DoesNotContain(made.ImageUrls[1], ev.ImageUrls);   // the one left out
        Assert.Null(ev.BannerImageUrl);                       // removed

        var replaced = await rig.Service().UpdateEventAsync("eli", "Community", "c2", made.Id, WithMedia(r => { r.ExistingImageUrls = ev.ImageUrls; r.BannerImage = Files.Webp("new.webp"); }));
        Assert.Equal(200, replaced.Code);
        Assert.EndsWith(".webp", (await rig.Db().Events.SingleAsync()).BannerImageUrl);
    }

    [Fact]
    public async Task An_edit_that_changes_nothing_about_pictures_leaves_the_banner_alone_but_drops_unlisted_photos()
    {
        var rig = await Seeded();
        var made = (await rig.Service().CreateEventAsync("eli", "Community", "c2", WithMedia(r => { r.BannerImage = Files.Png(); r.Images = [Files.Png("a.png")]; }))).Data!;

        await rig.Service().UpdateEventAsync("eli", "Community", "c2", made.Id, Event());

        var ev = await rig.Db().Events.SingleAsync();
        Assert.Equal(made.BannerImageUrl, ev.BannerImageUrl);   // no new banner and not removed: it stays
        Assert.Null(ev.ImageUrls);                              // the editor sends the photos it wants kept; none were listed
    }

    [Fact]
    public async Task An_edit_cannot_go_past_the_photo_limit_counting_the_ones_it_keeps()
    {
        var rig = await Seeded();
        var made = (await rig.Service().CreateEventAsync("eli", "Community", "c2", WithMedia(r => r.Images = Enumerable.Range(0, 7).Select(i => Files.Png($"{i}.png")).ToList()))).Data!;

        var over = await rig.Service().UpdateEventAsync("eli", "Community", "c2", made.Id, WithMedia(r => { r.ExistingImageUrls = made.ImageUrls; r.Images = [Files.Png("x.png"), Files.Png("y.png")]; }));

        Assert.Equal(400, over.Code);
        Assert.Equal(7, (await rig.Db().Events.SingleAsync()).ImageUrls!.Count);
    }
}

internal static class RigExtensions
{
    public static async Task<string> MemberStatus(this AlumniDbContext db, string id) => (await db.Set<MemberEntity>().SingleAsync(m => m.Id == id)).Status;
}
