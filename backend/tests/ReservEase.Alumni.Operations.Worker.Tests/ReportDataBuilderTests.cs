using ReservEase.Alumni.Operations.Worker.Workflows.Reports;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.Reports.Sdk.Models;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Operations.Worker.Tests;

public class ReportDataBuilderTests
{
    private const string Ours = "inst-ours";
    private const string Theirs = "inst-theirs";
    private static readonly int ThisYear = DateTime.UtcNow.Year;

    /// <summary>Seeds rows exactly as given (each carries its own InstitutionId) into a context with no tenant, as the worker runs.</summary>
    private static async Task<ReportDataBuilder> Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name))
        {
            db.AddRange(seed);
            await db.SaveChangesAsync();
        }
        return ReportRig.Builder(TestDb.Create(name));
    }

    private static Member Person(string id, int year = 2014, string status = "Active", string institution = Ours, string? jobTitle = null, string? location = null) => new()
    {
        Id = id, InstitutionId = institution, FirstName = id, LastName = "Mensah", Email = $"{id}@x.com",
        GraduationYear = year, Status = status, DepartmentId = "mining", JobTitle = jobTitle, Location = location,
    };

    private static ReportJob Job(string type, Dictionary<string, string>? parameters = null, bool scoped = false, List<int>? years = null, List<string>? communities = null) => new()
    {
        ReportType = type, InstitutionId = Ours, Audience = ReportAudiences.Institution, RequestedById = "admin-1",
        Parameters = parameters ?? [], RequesterIsScoped = scoped, ScopeYearGroups = years ?? [], ScopeCommunityIds = communities ?? [],
    };

    private static async Task<(ReportData Data, List<object?[]> Rows)> Run(ReportDataBuilder builder, ReportJob job)
    {
        var data = builder.Build(job);
        return (data, await ReportRig.Rows(data));
    }

    [Fact]
    public void An_unknown_report_type_is_refused_rather_than_producing_an_empty_file()
        => Assert.Throws<ReportNotSupportedException>(() => ReportRig.Builder(TestDb.Create()).Build(Job("no-such-report")));

    [Fact]
    public void Every_report_in_the_catalog_has_a_builder()
    {
        var builder = ReportRig.Builder(TestDb.Create());
        foreach (var definition in ReportCatalog.All)
            Assert.NotEmpty(builder.Build(new ReportJob { ReportType = definition.Key, InstitutionId = Ours }).Columns);
    }

    // ── Members ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_member_roster_never_includes_another_institutions_members()
    {
        var builder = await Create(Person("ama"), Person("kofi"), Person("stranger", institution: Theirs));

        var (data, rows) = await Run(builder, Job(ReportTypes.Members));

        Assert.Equal(["ama", "kofi"], ReportRig.Column(data, rows, "First name"));
    }

    [Fact]
    public async Task The_member_roster_shows_the_department_by_name_and_no_internal_ids()
    {
        var builder = await Create(Person("ama"), new Department { Id = "mining", InstitutionId = Ours, Name = "Mining Engineering" });

        var (data, rows) = await Run(builder, Job(ReportTypes.Members));

        Assert.Equal("Mining Engineering", ReportRig.Column(data, rows, "Department").Single());
        Assert.DoesNotContain(data.Columns, c => c.Header.Contains("Id", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Member_filters_narrow_the_roster_and_text_filters_ignore_case()
    {
        var builder = await Create(
            Person("nurse", 2010, jobTitle: "Senior Nurse", location: "Kumasi"),
            Person("engineer", 2010, jobTitle: "Engineer", location: "Kumasi"),
            Person("old-nurse", 1990, jobTitle: "Nurse", location: "Accra"),
            Person("pending-nurse", 2010, "Pending", jobTitle: "Nurse", location: "Kumasi"));

        var (data, rows) = await Run(builder, Job(ReportTypes.Members, new()
        {
            [ReportParameters.Status] = "Active", [ReportParameters.YearFrom] = "2000", [ReportParameters.YearTo] = "2020",
            [ReportParameters.Profession] = "nurse", [ReportParameters.Location] = "KUMASI",
        }));

        Assert.Equal(["nurse"], ReportRig.Column(data, rows, "First name"));
    }

    [Fact]
    public async Task A_scoped_admin_gets_only_their_year_groups_and_their_communities_approved_members()
    {
        var builder = await Create(
            Person("in-year", 2014), Person("other-year", 1999), Person("in-community", 1999), Person("pending-in-community", 1999),
            new CommunityMembership { InstitutionId = Ours, CommunityId = "c1", MemberId = "in-community", Status = "Approved" },
            new CommunityMembership { InstitutionId = Ours, CommunityId = "c1", MemberId = "pending-in-community", Status = "Pending" });

        var (data, rows) = await Run(builder, Job(ReportTypes.Members, scoped: true, years: [2014], communities: ["c1"]));

        Assert.Equal(["in-community", "in-year"], ReportRig.Column(data, rows, "First name").Order());
    }

    [Fact]
    public async Task A_roster_longer_than_one_page_comes_out_whole_and_in_order()
    {
        var people = Enumerable.Range(0, 1203).Select(i => (object)Person($"m{i:D4}")).ToArray();

        var (data, rows) = await Run(await Create(people), Job(ReportTypes.Members));

        var names = ReportRig.Column(data, rows, "First name");
        Assert.Equal(1203, names.Distinct().Count());
        Assert.Equal(names.Order(), names);
    }

    // ── Dues standing ───────────────────────────────────────────────────────

    private static Campaign Dues(string id, int year, List<int>? yearGroups = null, string institution = Ours) => new()
    {
        Id = id, InstitutionId = institution, Title = $"Dues {year}", IsMembershipCampaign = true, MembershipYear = year, YearGroups = yearGroups,
    };

    private static Contribution Paid(string memberId, string campaignId, decimal amount = 100, string status = "Successful", DateTime? on = null, string institution = Ours) => new()
    {
        InstitutionId = institution, MemberId = memberId, CampaignId = campaignId, Amount = amount, NetAmountToInstitution = amount,
        Status = status, PaymentMethod = "Paystack", ConfirmedAt = on ?? DateTime.UtcNow, CreatedAt = on ?? DateTime.UtcNow,
    };

    [Fact]
    public async Task Dues_standing_says_who_has_paid_this_year_and_who_still_owes()
    {
        var builder = await Create(
            Dues("dues", ThisYear), Person("paid"), Person("owing"), Person("only-tried"),
            Paid("paid", "dues", 150), Paid("only-tried", "dues", status: "Pending"));

        var (data, rows) = await Run(builder, Job(ReportTypes.DuesStanding));

        var standing = ReportRig.Column(data, rows, "First name").Zip(ReportRig.Column(data, rows, "Standing")).ToDictionary(x => x.First!, x => x.Second);
        Assert.Equal("Paid", standing["paid"]);
        Assert.Equal("Owing", standing["owing"]);
        Assert.Equal("Owing", standing["only-tried"]);
        Assert.Equal(150m, ReportRig.Column(data, rows, "Amount paid").Single(v => v is not null));
    }

    [Fact]
    public async Task Dues_standing_leaves_out_people_the_dues_do_not_fall_on()
    {
        var builder = await Create(
            Dues("dues", ThisYear, yearGroups: [2014]),
            Person("targeted", 2014), Person("other-class", 2010), Person("not-approved", 2014, "Pending"), Person("stranger", 2014, institution: Theirs));

        var (data, rows) = await Run(builder, Job(ReportTypes.DuesStanding));

        Assert.Equal(["targeted"], ReportRig.Column(data, rows, "First name"));
    }

    [Fact]
    public async Task Dues_standing_can_list_only_those_owing_and_only_counts_the_chosen_year()
    {
        var builder = await Create(
            Dues("this", ThisYear), Dues("last", ThisYear - 1), Person("paid-last-year-only"), Person("paid-this-year"),
            Paid("paid-last-year-only", "last"), Paid("paid-this-year", "this"));

        var (data, rows) = await Run(builder, Job(ReportTypes.DuesStanding, new() { [ReportParameters.Status] = "Owing" }));

        Assert.Equal(["paid-last-year-only"], ReportRig.Column(data, rows, "First name"));
    }

    [Fact]
    public async Task With_no_dues_set_for_the_year_the_standing_report_is_empty_not_everyone_owing()
        => Assert.Empty((await Run(await Create(Person("ama")), Job(ReportTypes.DuesStanding))).Rows);

    // ── Payments ────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_payments_ledger_covers_whole_days_at_both_ends_of_the_range()
    {
        var fund = new Campaign { Id = "fund", InstitutionId = Ours, Title = "Lab fund" };
        DateTime At(int day, int hour) => new(2026, 3, day, hour, 0, 0, DateTimeKind.Utc);
        var builder = await Create(fund, Person("ama"),
            Paid("ama", "fund", 1, on: At(9, 23)), Paid("ama", "fund", 2, on: At(10, 0)),
            Paid("ama", "fund", 3, on: At(11, 23)), Paid("ama", "fund", 4, on: At(12, 0)));

        var (data, rows) = await Run(builder, Job(ReportTypes.Payments, new() { [ReportParameters.From] = "2026-03-10", [ReportParameters.To] = "2026-03-11" }));

        Assert.Equal([2m, 3m], ReportRig.Column(data, rows, "Amount").Order());
    }

    [Fact]
    public async Task The_payments_ledger_names_the_payer_and_the_fundraiser_and_marks_dues()
    {
        var builder = await Create(
            new Campaign { Id = "fund", InstitutionId = Ours, Title = "Lab fund" }, Dues("dues", ThisYear), Person("ama"),
            Paid("ama", "fund"), Paid("ama", "dues"), Paid("ama", "fund", institution: Theirs));

        var (data, rows) = await Run(builder, Job(ReportTypes.Payments));

        Assert.Equal(2, rows.Count);
        Assert.All(ReportRig.Column(data, rows, "Paid by"), name => Assert.Equal("ama Mensah", name));
        Assert.Equal(["Dues", "Fundraiser"], ReportRig.Column(data, rows, "Kind").Order());
        Assert.Contains("Lab fund", ReportRig.Column(data, rows, "Paid towards"));
    }

    [Fact]
    public async Task The_payments_ledger_never_has_a_fee_or_net_column_because_institutions_receive_their_full_amount()
    {
        var builder = await Create(new Campaign { Id = "fund", InstitutionId = Ours, Title = "Lab fund" }, Person("ama"),
            Paid("ama", "fund", 100), Paid("ama", "fund", status: "Pending"));

        var (data, rows) = await Run(builder, Job(ReportTypes.Payments));

        var headers = data.Columns.Select(c => c.Header).ToList();
        Assert.Contains("Amount", headers);
        Assert.DoesNotContain(headers, h => System.Text.RegularExpressions.Regex.IsMatch(h, "(?i)fee|net|reached|deduct|charge|commission"));
        Assert.Equal(2, rows.Count);
    }

    // ── Platform ────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_institutions_overview_counts_each_institutions_own_members_and_money()
    {
        var builder = await Create(
            new Institution { Id = Ours, Name = "Alpha", Slug = "alpha" }, new Institution { Id = Theirs, Name = "Beta", Slug = "beta" },
            Person("a1"), Person("a2", status: "Pending"), Person("b1", institution: Theirs),
            Paid("a1", "c", 100), Paid("a1", "c", 50, status: "Pending"), Paid("b1", "c", 30, institution: Theirs));

        var data = builder.Build(new ReportJob { ReportType = ReportTypes.PlatformInstitutions, Audience = ReportAudiences.Platform });
        var rows = await ReportRig.Rows(data);

        Assert.Equal(["Alpha", "Beta"], ReportRig.Column(data, rows, "Institution"));
        Assert.Equal([2, 1], ReportRig.Column(data, rows, "Members"));
        Assert.Equal([1, 1], ReportRig.Column(data, rows, "Approved members"));
        Assert.Equal([100m, 30m], ReportRig.Column(data, rows, "Collected, all time"));
    }

    [Fact]
    public async Task Revenue_by_institution_has_one_row_per_institution_per_month_of_successful_payments()
    {
        var march = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc);
        var builder = await Create(
            new Institution { Id = Ours, Name = "Alpha", Slug = "alpha" },
            Paid("a", "c", 100, on: march), Paid("a", "c", 25, on: march.AddDays(3)), Paid("a", "c", 40, on: march.AddMonths(1)), Paid("a", "c", 999, "Failed", on: march),
            new StoreOrder { InstitutionId = Ours, Status = "Successful", TotalAmount = 60, PlatformFeeAmount = 3, ConfirmedAt = march, CreatedAt = march });

        var data = builder.Build(new ReportJob
        {
            ReportType = ReportTypes.PlatformRevenue, Audience = ReportAudiences.Platform,
            Parameters = new() { [ReportParameters.From] = "2026-01-01", [ReportParameters.To] = "2026-12-31" },
        });
        var rows = await ReportRig.Rows(data);

        Assert.Equal(["Mar 2026", "Apr 2026"], ReportRig.Column(data, rows, "Month"));
        Assert.Equal([185m, 40m], ReportRig.Column(data, rows, "Collected"));
        Assert.Equal([60m, 0m], ReportRig.Column(data, rows, "Store"));
        Assert.Equal([3, 1], ReportRig.Column(data, rows, "Payments"));
    }
}
