using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.PostgresDb.Tests;

public class AudienceSegmentResolverTests
{
    private const string Tenant = "i1";
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    private static Member Person(string id, int year = 0, string status = "Active", DateTime? lastLogin = null, DateTime? created = null, Dictionary<string, string>? answers = null)
        => new() { Id = id, InstitutionId = Tenant, FirstName = id, LastName = "X", Email = $"{id}@x.com", GraduationYear = year, Status = status, LastLoginAt = lastLogin, CreatedAt = created ?? Now.AddYears(-1), CustomFields = answers };

    private static AudienceSegmentResolver Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant))
        {
            foreach (var e in seed) db.Add(e);
            db.SaveChanges();
        }
        var ctx = TestDb.Create(name, Tenant);
        return new AudienceSegmentResolver(
            new AlumniPgRepository<Member>(ctx), new AlumniPgRepository<CommunityMembership>(ctx), new AlumniPgRepository<Community>(ctx),
            new AlumniPgRepository<Contribution>(ctx), new AlumniPgRepository<Campaign>(ctx));
    }

    private static CommunityMembership In(string group, string member, string status = "Approved")
        => new() { Id = $"{group}-{member}", InstitutionId = Tenant, CommunityId = group, MemberId = member, Status = status, Role = "Member" };

    private static async Task<List<string>> Ids(AudienceSegmentResolver r, SegmentCriteria c, SegmentScope? scope = null)
        => (await r.ResolveAsync(c, scope ?? SegmentScope.Everyone, Now)).Select(m => m.Id).OrderBy(x => x).ToList();

    [Fact]
    public async Task A_group_kind_reaches_every_group_of_that_kind_but_only_approved_members()
    {
        var r = Create(
            Person("ama"), Person("kofi"), Person("esi"), Person("yaw"),
            new Community { Id = "kumasi", InstitutionId = Tenant, Name = "Kumasi", Kind = "Chapter" },
            new Community { Id = "accra", InstitutionId = Tenant, Name = "Accra", Kind = "chapter" },   // spelling case differs
            new Community { Id = "finance", InstitutionId = Tenant, Name = "Finance", Kind = "Committee" },
            In("kumasi", "ama"), In("accra", "kofi"), In("finance", "esi"), In("kumasi", "yaw", status: "Pending"));

        var ids = await Ids(r, new SegmentCriteria { GroupKinds = ["Chapter"] });

        Assert.Equal(["ama", "kofi"], ids);
    }

    [Fact]
    public async Task Dues_unpaid_is_judged_against_this_years_membership_campaign()
    {
        var r = Create(
            Person("paid"), Person("owes"),
            new Campaign { Id = "dues-2026", InstitutionId = Tenant, Title = "2026 Dues", IsMembershipCampaign = true, MembershipYear = 2026 },
            new Campaign { Id = "dues-2025", InstitutionId = Tenant, Title = "2025 Dues", IsMembershipCampaign = true, MembershipYear = 2025 },
            new Contribution { Id = "c1", InstitutionId = Tenant, CampaignId = "dues-2026", MemberId = "paid", Status = "Successful" },
            new Contribution { Id = "c2", InstitutionId = Tenant, CampaignId = "dues-2025", MemberId = "owes", Status = "Successful" });  // last year's does not count

        Assert.Equal(["owes"], await Ids(r, new SegmentCriteria { DuesStatus = "Unpaid" }));
        Assert.Equal(["paid"], await Ids(r, new SegmentCriteria { DuesStatus = "Paid" }));
    }

    [Fact]
    public async Task With_no_dues_set_for_the_year_nobody_is_unpaid_and_nobody_is_paid()
    {
        var r = Create(Person("ama"));
        Assert.Empty(await Ids(r, new SegmentCriteria { DuesStatus = "Unpaid" }));
        Assert.Empty(await Ids(r, new SegmentCriteria { DuesStatus = "Paid" }));
    }

    [Fact]
    public async Task Rules_combine_and_each_one_narrows()
    {
        var r = Create(
            Person("old-active", 2010, lastLogin: Now.AddDays(-5), answers: new() { ["chapter"] = "Kumasi" }),
            Person("old-dormant", 2010, lastLogin: Now.AddDays(-200), answers: new() { ["chapter"] = "Kumasi" }),
            Person("new-active", 2024, lastLogin: Now.AddDays(-1), created: Now.AddDays(-10), answers: new() { ["chapter"] = "Accra" }),
            Person("suspended", 2010, status: "Suspended", lastLogin: Now.AddDays(-400)));

        Assert.Equal(["new-active", "old-active", "old-dormant"], await Ids(r, new SegmentCriteria { Status = "Active" }));
        Assert.Equal(["old-dormant"], await Ids(r, new SegmentCriteria { Status = "Active", DormantOnly = true, GraduationYearTo = 2015 }));
        Assert.Equal(["new-active"], await Ids(r, new SegmentCriteria { Status = "Active", JoinedWithinDays = 30 }));
        Assert.Equal(["old-active", "old-dormant"], await Ids(r, new SegmentCriteria { AnswerContains = "kuma" }));
    }

    [Fact]
    public async Task A_scoped_administrator_never_reaches_beyond_their_year_groups_and_groups()
    {
        var r = Create(
            Person("in-year", 2015), Person("in-group", 2001), Person("outside", 2002),
            new Community { Id = "g1", InstitutionId = Tenant, Name = "G1" },
            In("g1", "in-group"));

        var scope = new SegmentScope(false, [2015], ["g1"]);

        Assert.Equal(["in-group", "in-year"], await Ids(r, new SegmentCriteria(), scope));
        Assert.Equal(["in-group", "in-year", "outside"], await Ids(r, new SegmentCriteria()));
    }

    [Fact]
    public async Task A_group_rule_with_no_matching_groups_describes_no_one_rather_than_everyone()
    {
        var r = Create(Person("ama"));
        Assert.Empty(await Ids(r, new SegmentCriteria { GroupKinds = ["Nothing like this"] }));
    }

    // ── The quick count must always agree with the full route ───────────

    public static IEnumerable<object[]> Rules() =>
    [
        [new SegmentCriteria()],
        [new SegmentCriteria { Status = "Pending" }],
        [new SegmentCriteria { GraduationYearFrom = 2010, GraduationYearTo = 2015 }],
        [new SegmentCriteria { DormantOnly = true }],
        [new SegmentCriteria { JoinedWithinDays = 90 }],
        [new SegmentCriteria { GroupIds = ["kumasi"] }],
        [new SegmentCriteria { GroupKinds = ["chapter"] }],
        [new SegmentCriteria { GroupKinds = ["Nothing"] }],
        [new SegmentCriteria { DuesStatus = "Unpaid" }],
        [new SegmentCriteria { AnswerContains = "Tema" }],
    ];

    [Theory]
    [MemberData(nameof(Rules))]
    public async Task The_quick_count_matches_resolving_everyone_for_every_kind_of_rule(SegmentCriteria rule)
    {
        var r = Create(
            Person("a", 2008, lastLogin: Now.AddDays(-5), created: Now.AddDays(-400)), Person("b", 2011, lastLogin: Now.AddDays(-200), created: Now.AddDays(-30)),
            Person("c", 2013, status: "Pending", created: Now.AddDays(-10)), Person("d", 2015, answers: new() { ["home"] = "Tema" }),
            Person("e", 2020, lastLogin: Now.AddDays(-1)), Person("f", 2012, status: "Deleted"),
            new Community { Id = "kumasi", InstitutionId = Tenant, Name = "Kumasi", Kind = "Chapter" },
            In("kumasi", "b"), In("kumasi", "d"), In("kumasi", "e", status: "Pending"),
            new Campaign { Id = "dues", InstitutionId = Tenant, Title = "Dues", IsMembershipCampaign = true, MembershipYear = 2026 },
            new Contribution { Id = "c1", InstitutionId = Tenant, CampaignId = "dues", MemberId = "a", Status = "Successful" });

        var full = await Ids(r, rule);
        var (count, sample) = await r.CountAsync(rule, SegmentScope.Everyone, 3, Now);

        Assert.Equal(full.Count, count);
        Assert.Equal(Math.Min(3, full.Count), sample.Count);
        Assert.All(sample, m => Assert.Contains(m.Id, full));
    }

    [Fact]
    public async Task The_quick_count_respects_what_a_scoped_administrator_may_reach()
    {
        var r = Create(
            Person("a", 2010), Person("b", 2011), Person("c", 2012),
            new Community { Id = "g1", InstitutionId = Tenant, Name = "G1" },
            In("g1", "c"));
        var scope = new SegmentScope(false, [2010], ["g1"]);

        var full = await Ids(r, new SegmentCriteria(), scope);
        var (count, _) = await r.CountAsync(new SegmentCriteria(), scope, 0, Now);

        Assert.Equal(["a", "c"], full);
        Assert.Equal(2, count);
    }
}
