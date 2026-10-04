using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

public class HomeServiceTests
{
    private const string Tenant = "inst-1";
    private const string Me = "me";
    private static readonly string[] NothingDisabled = [];

    private static HomeService Service(AlumniDbContext ctx) => new(
        new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<CommunityMembership>(ctx), new AlumniPgRepository<Community>(ctx),
        new AlumniPgRepository<Spotlight>(ctx), new AlumniPgRepository<ForumThread>(ctx), new AlumniPgRepository<MentorProfile>(ctx),
        new AlumniPgRepository<BusinessListing>(ctx), new AlumniPgRepository<Job>(ctx), new AlumniPgRepository<AlumniEvent>(ctx),
        new AlumniPgRepository<NewsPost>(ctx), new AlumniPgRepository<Resource>(ctx), new AlumniPgRepository<PhotoAlbum>(ctx),
        new AlumniPgRepository<StoreProduct>(ctx), new AlumniPgRepository<StoreOrder>(ctx), new AlumniPgRepository<ServiceType>(ctx),
        NullLogger<HomeService>.Instance);

    /// <summary>Seeds the given rows plus the calling member ("me", class of 2014, Mining) unless the seed already has one.</summary>
    private static async Task<HomeService> Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant))
        {
            if (!seed.OfType<MemberEntity>().Any(m => m.Id == Me)) db.Add(Person(Me, 2014, daysAgo: 400));
            foreach (var e in seed) { if (e is ITenantScoped t) t.InstitutionId = Tenant; db.Add(e); }
            await db.SaveChangesAsync();
        }
        return Service(TestDb.Create(name, Tenant));
    }

    private static MemberEntity Person(string id, int year, string status = "Active", string department = "mining", int daysAgo = 1) => new()
    {
        Id = id, GraduationYear = year, Status = status, DepartmentId = department,
        FirstName = id, LastName = "Mensah", Email = $"{id}@x.com", CreatedAt = DateTime.UtcNow.AddDays(-daysAgo),
    };

    private static ForumThread Thread(string id, string authorId, string? communityId = null, int daysAgo = 1) => new()
    {
        Id = id, AuthorId = authorId, Title = $"Thread {id}", CategoryId = "general", CommunityId = communityId, CreatedAt = DateTime.UtcNow.AddDays(-daysAgo),
    };

    private static async Task<List<HomeFeedItemDto>> Feed(HomeService service, params string[] disabled)
        => (await service.GetFeedAsync(Me, disabled)).Data!.Items;

    private static async Task<List<string>> EmptyModules(HomeService service, params string[] disabled)
        => (await service.GetModulesAsync(Me, disabled)).Data!.Empty;

    // ── Feed ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task With_nothing_happening_the_feed_is_empty()
        => Assert.Empty(await Feed(await Create()));

    [Fact]
    public async Task A_new_member_appears_by_name_and_is_marked_as_a_classmate_when_they_share_my_year()
    {
        var service = await Create(Person("kofi", 2014), Person("ama", 2009, department: "geology"));

        var feed = await Feed(service);

        var kofi = feed.Single(i => i.PersonId == "kofi");
        Assert.Equal((HomeFeedKinds.MemberJoined, "kofi Mensah", 2014, true, true),
            (kofi.Kind, kofi.PersonName, kofi.PersonGraduationYear, kofi.SameYearGroup, kofi.SameDepartment));
        var ama = feed.Single(i => i.PersonId == "ama");
        Assert.Equal((false, false), (ama.SameYearGroup, ama.SameDepartment));
    }

    [Fact]
    public async Task I_never_appear_in_my_own_feed()
    {
        var service = await Create(Person(Me, 2014, daysAgo: 1), Thread("t1", Me));

        Assert.Empty(await Feed(service));
    }

    [Fact]
    public async Task Members_without_a_graduation_year_are_not_treated_as_one_class()
    {
        var service = await Create(Person(Me, 0, daysAgo: 400), Person("kofi", 0));

        var kofi = (await Feed(service)).Single();

        Assert.Equal((false, (int?)null), (kofi.SameYearGroup, kofi.PersonGraduationYear));
    }

    [Fact]
    public async Task People_who_are_not_active_members_are_left_out_along_with_what_they_did()
    {
        var service = await Create(
            Person("pending", 2014, "Pending"), Person("banned", 2014, "Banned", daysAgo: 300),
            Thread("t1", "banned"), Thread("t2", "deleted-member"));

        Assert.Empty(await Feed(service));
    }

    [Fact]
    public async Task Activity_older_than_the_window_is_left_out()
    {
        var service = await Create(Person("old", 2014, daysAgo: 300), Thread("stale", "old", daysAgo: 90), Thread("fresh", "old", daysAgo: 5));

        var only = Assert.Single(await Feed(service));

        Assert.Equal("fresh", only.EntityId);
    }

    [Fact]
    public async Task The_feed_is_newest_first_across_kinds()
    {
        var service = await Create(
            Person("kofi", 2014, daysAgo: 300),
            Thread("t1", "kofi", daysAgo: 3),
            new MentorProfile { Id = "m1", MemberId = "kofi", Area = "Mine planning", Status = "Approved", MaxMentees = 3, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new BusinessListing { Id = "b1", MemberId = "kofi", BusinessName = "Kofi Drilling", Status = "Approved", CreatedAt = DateTime.UtcNow.AddDays(-2) });

        var feed = await Feed(service);

        Assert.Equal([HomeFeedKinds.MentorJoined, HomeFeedKinds.BusinessListed, HomeFeedKinds.ForumThread], feed.Select(i => i.Kind));
        Assert.Equal(["Mine planning", "Kofi Drilling", "Thread t1"], feed.Select(i => i.Title));
    }

    [Fact]
    public async Task Only_approved_visible_member_owned_records_make_the_feed()
    {
        var service = await Create(
            Person("kofi", 2014, daysAgo: 300),
            new BusinessListing { Id = "pending", MemberId = "kofi", BusinessName = "Pending", Status = "Pending" },
            new BusinessListing { Id = "hidden", MemberId = "kofi", BusinessName = "Hidden", Status = "Approved", IsHiddenByMember = true },
            new BusinessListing { Id = "admin", MemberId = null, BusinessName = "Admin-added", Status = "Approved" },
            new MentorProfile { Id = "m-pending", MemberId = "kofi", Area = "x", Status = "Pending", MaxMentees = 3 },
            new MentorProfile { Id = "m-full", MemberId = "kofi", Area = "x", Status = "Approved", MaxMentees = 1, CurrentMenteeCount = 1 },
            new Spotlight { Id = "s-pending", MemberId = "kofi", Title = "x", Status = "Pending" },
            new ForumThread { Id = "closed", AuthorId = "kofi", Title = "x", IsClosed = true });

        Assert.Empty(await Feed(service));
    }

    [Fact]
    public async Task A_community_thread_shows_only_to_approved_members_of_that_community()
    {
        var seed = new object[] { Person("kofi", 2014, daysAgo: 300), Thread("t1", "kofi", communityId: "c1") };

        Assert.Empty(await Feed(await Create(seed)));
        Assert.Empty(await Feed(await Create([.. seed, new CommunityMembership { CommunityId = "c1", MemberId = Me, Status = "Pending" }])));
        Assert.Single(await Feed(await Create([.. seed, new CommunityMembership { CommunityId = "c1", MemberId = Me, Status = "Approved" }])));
    }

    [Fact]
    public async Task A_shared_birthday_names_each_celebrant_once_and_skips_their_shoutout_threads()
    {
        var service = await Create(
            Person("kofi", 2014, daysAgo: 300), Person("ama", 2014, daysAgo: 300),
            new Spotlight { Id = "bday", Type = "Birthday", Status = "Approved", MemberId = "kofi", MemberIds = ["kofi", "ama"], ForumThreadIds = ["shout-1", "shout-2"], Title = "Happy birthday" },
            Thread("shout-1", "kofi"), Thread("shout-2", "ama"));

        var feed = await Feed(service);

        Assert.Equal(["ama", "kofi"], feed.Select(i => i.PersonId).Order());
        Assert.All(feed, i => Assert.Equal(HomeFeedKinds.Birthday, i.Kind));
    }

    [Fact]
    public async Task A_switched_off_feature_contributes_nothing_to_the_feed()
    {
        var service = await Create(Person("kofi", 2014), Thread("t1", "kofi"));

        Assert.Equal(2, (await Feed(service)).Count);
        Assert.Equal([HomeFeedKinds.MemberJoined], (await Feed(service, InstitutionFeatures.Forum)).Select(i => i.Kind));
        Assert.Equal([HomeFeedKinds.ForumThread], (await Feed(service, InstitutionFeatures.Directory)).Select(i => i.Kind));
    }

    [Fact]
    public async Task With_more_joiners_than_the_cap_classmates_are_the_ones_kept()
    {
        var others = Enumerable.Range(0, 12).Select(i => (object)Person($"other{i}", 1999));
        var service = await Create([.. others, Person("classmate", 2014, daysAgo: 30)]);

        Assert.Contains(await Feed(service), i => i.PersonId == "classmate");
    }

    [Fact]
    public async Task The_feed_reports_when_i_last_saw_home()
    {
        var seen = DateTime.UtcNow.AddDays(-3);
        var me = Person(Me, 2014, daysAgo: 400);
        me.HomeSeenAt = seen;

        Assert.Equal(seen, (await (await Create(me)).GetFeedAsync(Me, NothingDisabled)).Data!.LastSeenAt);
        Assert.Null((await (await Create()).GetFeedAsync(Me, NothingDisabled)).Data!.LastSeenAt);
    }

    [Fact]
    public async Task An_unknown_member_gets_not_found()
    {
        var service = await Create();

        Assert.Equal(404, (await service.GetFeedAsync("nobody", NothingDisabled)).Code);
        Assert.Equal(404, (await service.GetModulesAsync("nobody", NothingDisabled)).Code);
    }

    // ── Modules ─────────────────────────────────────────────────────────────

    private static readonly string[] CheckedModules =
    [
        InstitutionFeatures.Jobs, InstitutionFeatures.Events, InstitutionFeatures.News, InstitutionFeatures.Forum,
        InstitutionFeatures.Mentorship, InstitutionFeatures.Resources, InstitutionFeatures.PhotoAlbums, InstitutionFeatures.BusinessDirectory,
        InstitutionFeatures.Spotlights, InstitutionFeatures.Store, InstitutionFeatures.Services, InstitutionFeatures.Communities,
    ];

    [Fact]
    public async Task In_a_brand_new_institution_every_content_module_is_empty()
        => Assert.Equal(CheckedModules.Order(), (await EmptyModules(await Create())).Order());

    [Fact]
    public async Task A_module_with_content_i_can_see_is_not_empty()
    {
        var service = await Create(
            new Job { Title = "Engineer", Status = "Active" },
            new AlumniEvent { Title = "Reunion", Status = "Completed" },
            new NewsPost { Title = "News", Status = "Published" },
            Thread("t1", "someone"),
            new MentorProfile { MemberId = "someone", Status = "Approved" },
            new Resource { Title = "Guide" },
            new PhotoAlbum { Title = "Congregation" },
            new BusinessListing { BusinessName = "Shop", Status = "Approved" },
            new Spotlight { MemberId = "someone", Status = "Approved" },
            new StoreProduct { Status = "Active" },
            new ServiceType { Status = "Active" },
            new Community { Name = "Mining alumni" });

        Assert.Empty(await EmptyModules(service));
    }

    [Fact]
    public async Task Content_i_am_not_allowed_to_see_does_not_count()
    {
        var service = await Create(
            new Job { Title = "Other class only", Status = "Active", YearGroups = [1999] },
            new Job { Title = "Closed", Status = "Closed" },
            new AlumniEvent { Title = "Cancelled", Status = "Cancelled" },
            new AlumniEvent { Title = "Another community", Status = "Upcoming", CommunityId = "not-mine" },
            new NewsPost { Title = "Draft", Status = "Draft" },
            Thread("t1", "someone", communityId: "not-mine"),
            new MentorProfile { MemberId = "someone", Status = "Pending" },
            new Resource { Title = "Community-only", CommunityId = "not-mine" },
            new PhotoAlbum { Title = "Other class", YearGroups = [1999] },
            new BusinessListing { BusinessName = "Hidden", Status = "Approved", IsHiddenByMember = true },
            new Spotlight { MemberId = "someone", Status = "Rejected" },
            new StoreProduct { Status = "Draft" },
            new ServiceType { Status = "Archived" },
            new Community { Name = "Retired", IsActive = false });

        Assert.Equal(CheckedModules.Order(), (await EmptyModules(service)).Order());
    }

    [Fact]
    public async Task Content_targeted_at_my_year_or_my_community_counts()
    {
        var service = await Create(
            new CommunityMembership { CommunityId = "mine", MemberId = Me, Status = "Approved" },
            new Job { Title = "My class", Status = "Active", YearGroups = [2014] },
            new AlumniEvent { Title = "My community", Status = "Upcoming", CommunityId = "mine" },
            new PhotoAlbum { Title = "My class", YearGroups = [2014] });

        var empty = await EmptyModules(service);

        Assert.DoesNotContain(InstitutionFeatures.Jobs, empty);
        Assert.DoesNotContain(InstitutionFeatures.Events, empty);
        Assert.DoesNotContain(InstitutionFeatures.PhotoAlbums, empty);
    }

    [Fact]
    public async Task A_switched_off_feature_is_not_reported_as_empty()
        => Assert.DoesNotContain(InstitutionFeatures.Forum, await EmptyModules(await Create(), InstitutionFeatures.Forum));

    [Fact]
    public async Task Store_orders_are_reported_only_for_the_member_who_placed_them()
    {
        var mine = await Create(new StoreOrder { MemberId = Me });
        var someoneElses = await Create(new StoreOrder { MemberId = "kofi" });

        Assert.True((await mine.GetModulesAsync(Me, NothingDisabled)).Data!.HasStoreOrders);
        Assert.False((await someoneElses.GetModulesAsync(Me, NothingDisabled)).Data!.HasStoreOrders);
    }

    // ── Seen marker ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Marking_home_seen_moves_my_marker_and_nobody_elses()
    {
        var (db, connection) = TestDb.CreateRelational(Tenant);
        using var _ = connection;
        db.AddRange(new Institution { Id = Tenant }, Stamp(Person(Me, 2014)), Stamp(Person("kofi", 2014)));
        await db.SaveChangesAsync();
        var before = DateTime.UtcNow;

        await Service(db).MarkSeenAsync(Me);

        var members = new AlumniPgRepository<MemberEntity>(TestDb.OpenRelational(connection, Tenant));
        Assert.True((await members.GetByIdAsync(Me))!.HomeSeenAt >= before);
        Assert.Null((await members.GetByIdAsync("kofi"))!.HomeSeenAt);

        static MemberEntity Stamp(MemberEntity m) { m.InstitutionId = Tenant; return m; }
    }
}
