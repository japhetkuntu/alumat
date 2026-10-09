using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.Temporal.Sdk;
using Moq;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

public class NextStepRuleTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    private static NextStepFacts Everything() => new() { JobsEnabled = true, EventsEnabled = true, ForumEnabled = true };

    [Fact]
    public void A_member_with_nothing_to_do_is_only_offered_the_quiet_invitation_to_share_an_opportunity()
        => Assert.Equal(["share-opportunity"], NextStepRules.Evaluate(Everything(), Now).Select(s => s.Key));

    [Fact]
    public void A_thin_profile_comes_first_and_names_exactly_what_is_missing()
    {
        var steps = NextStepRules.Evaluate(Everything() with { MissingProfileParts = ["photo", "work", "short bio"] }, Now);

        Assert.Equal("complete-profile", steps[0].Key);
        Assert.Contains("photo, work and short bio", steps[0].Detail);
    }

    [Fact]
    public void An_upcoming_event_is_named_with_how_soon_it_is()
    {
        var tomorrow = NextStepRules.Evaluate(Everything() with { NextEventNotSignedUpFor = ("e1", "Homecoming", Now.AddHours(20)) }, Now).Single(s => s.Key == "sign-up-event");

        Assert.Equal("Sign up for Homecoming", tomorrow.Title);
        Assert.Equal("It is tomorrow.", tomorrow.Detail);
        Assert.Equal("/events/e1", tomorrow.ActionUrl);
    }

    [Fact]
    public void Never_more_than_three_steps_and_the_most_useful_win()
    {
        var steps = NextStepRules.Evaluate(Everything() with
        {
            MissingProfileParts = ["photo"], NextEventNotSignedUpFor = ("e1", "X", Now.AddDays(3)), NewOpportunities = 4, RecentDiscussions = 5,
        }, Now);

        Assert.Equal(["complete-profile", "sign-up-event", "new-opportunities"], steps.Select(s => s.Key));
    }

    [Fact]
    public void Someone_already_in_the_discussions_is_not_asked_to_join_them()
        => Assert.DoesNotContain(NextStepRules.Evaluate(Everything() with { RecentDiscussions = 5, PostedInDiscussionsRecently = true }, Now), s => s.Key == "join-discussion");

    [Fact]
    public void Switched_off_features_produce_no_steps_for_them()
    {
        var steps = NextStepRules.Evaluate(new NextStepFacts { NextEventNotSignedUpFor = ("e1", "X", Now.AddDays(2)), NewOpportunities = 3, RecentDiscussions = 3 }, Now);

        Assert.Empty(steps);
    }

    [Fact]
    public void No_step_ever_asks_for_money()
        => Assert.All(NextStepRules.Evaluate(Everything() with { MissingProfileParts = ["photo"], NextEventNotSignedUpFor = ("e", "X", Now.AddDays(2)), NewOpportunities = 1, RecentDiscussions = 1 }, Now),
            s => Assert.DoesNotMatch("(?i)pay|give|donat|contribut", s.Title + s.ActionUrl));
}

public class OpportunitySuggestionTests
{
    private const string Tenant = "inst-1";
    private const string Other = "inst-2";

    // Temporal is "unavailable" in tests, so an enqueued alert shows up as a logged "dropped notification" line we can assert on.
    private static readonly CapturingLogger<MemberJobService> Alerts = new();
    private static MemberJobService Service(AlumniDbContext ctx, ILogger<MemberJobService>? log = null) => new(
        new AlumniPgRepository<Job>(ctx), new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<CommunityMembership>(ctx),
        new AlumniPgRepository<Community>(ctx), Mock.Of<ITemporalClientProvider>(t => t.IsAvailable == false), TestDb.Tenant(Tenant), log ?? NullLogger<MemberJobService>.Instance);

    private static (MemberJobService svc, string db) Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant))
        {
            db.Add(new MemberEntity { Id = "me", InstitutionId = Tenant, GraduationYear = 2014, Status = "Active", FirstName = "A", LastName = "B", Email = "a@x.com" });
            foreach (var e in seed) db.Add(e);
            db.SaveChanges();
        }
        return (Service(TestDb.Create(name, Tenant)), name);
    }

    private static SuggestOpportunityRequest Good(Action<SuggestOpportunityRequest>? tweak = null)
    {
        var r = new SuggestOpportunityRequest { Title = "Graduate engineer", Company = "Gold Fields", Location = "Tarkwa", Type = "Internship", ApplyUrl = "https://example.com/apply" };
        tweak?.Invoke(r);
        return r;
    }

    private static Job Posting(string title, string status = "Active", DateTime? deadline = null, string tenant = Tenant, string postedBy = "staff") =>
        new() { InstitutionId = tenant, Title = title, Company = "Co", Location = "Accra", Type = "Full-time", Status = status, Deadline = deadline, PostedBy = postedBy };

    private static async Task<List<string>> Visible(MemberJobService s) =>
        (await s.GetJobsAsync(new JobFilter { Page = 1, PageSize = 50 }, "me")).Data!.Results.Select(j => j.Title).ToList();

    [Fact]
    public async Task A_suggestion_alerts_the_administrators_and_a_rejected_one_does_not()
    {
        var name = TestDb.NewName();
        using (var seed = TestDb.Create(name, Tenant)) { seed.Add(new MemberEntity { Id = "me", InstitutionId = Tenant, GraduationYear = 2014, Status = "Active", FirstName = "A", LastName = "B", Email = "a@x.com" }); seed.SaveChanges(); }
        var log = new CapturingLogger<MemberJobService>();
        var s = Service(TestDb.Create(name, Tenant), log);

        Assert.Equal(400, (await s.SuggestAsync("me", Good(r => r.Title = "x"))).Code);
        Assert.DoesNotContain(log.Entries, e => e.Message.Contains("RequestAwaitingReview"));

        Assert.Equal(201, (await s.SuggestAsync("me", Good())).Code);
        Assert.Contains(log.Entries, e => e.Message.Contains("RequestAwaitingReview") && e.Message.Contains(Tenant));
    }

    [Fact]
    public void Every_review_alert_names_the_member_the_date_and_the_page_that_handles_it()
    {
        var now = new DateTime(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc);
        var all = new[]
        {
            ReviewAlerts.SuggestedOpportunity("i", "j1", "Ama Mensah", "Internship", now),
            ReviewAlerts.BusinessSubmitted("i", "b1", "Ama Mensah", "Kofi Bakes", now),
            ReviewAlerts.BusinessChanged("i", "b1", "Ama Mensah", "Kofi Bakes", false, now),
            ReviewAlerts.BusinessChanged("i", "b1", "Ama Mensah", "Kofi Bakes", true, now),
            ReviewAlerts.SpotlightSubmitted("i", "s1", "Ama Mensah", "Won an award", now),
            ReviewAlerts.MentorProfileSubmitted("i", "p1", "Ama Mensah", false, now),
        };
        Assert.All(all, a =>
        {
            Assert.Equal(NotificationKind.RequestAwaitingReview, a.Kind);
            Assert.Contains("Ama Mensah", a.NotificationMessage);
            Assert.Contains("9 Oct 2026", a.NotificationMessage);
            Assert.EndsWith("?status=Pending", a.ActionPath);
            Assert.NotNull(a.RequestId);
        });
        Assert.Equal(["/jobs", "/business-directory", "/business-directory", "/business-directory", "/spotlights", "/mentorship"], all.Select(a => a.ActionPath!.Split('?')[0]));
        Assert.Contains("A member", ReviewAlerts.SpotlightSubmitted("i", "s", " ", "T", now).NotificationMessage);
    }

    [Fact]
    public async Task A_suggestion_is_stored_as_pending_and_attributed_to_the_member()
    {
        var (s, db) = Create();

        var r = await s.SuggestAsync("me", Good());

        Assert.Equal(201, r.Code);
        using var check = TestDb.Create(db, Tenant);
        var job = check.Set<Job>().Single();
        Assert.Equal(("Pending", "me"), (job.Status, job.PostedBy));
    }

    [Fact]
    public async Task A_pending_suggestion_is_invisible_to_every_member_including_the_one_who_sent_it()
    {
        var (s, _) = Create();
        await s.SuggestAsync("me", Good());

        Assert.Empty(await Visible(s));
    }

    [Fact]
    public async Task A_closing_date_that_has_passed_takes_an_opportunity_out_of_the_list_but_today_is_still_open()
    {
        var (s, _) = Create(Posting("Open", deadline: DateTime.UtcNow.Date), Posting("Closed", deadline: DateTime.UtcNow.Date.AddDays(-1)), Posting("No date"));

        var shown = await Visible(s);

        Assert.Contains("Open", shown); Assert.Contains("No date", shown); Assert.DoesNotContain("Closed", shown);
    }

    [Fact]
    public async Task At_most_three_suggestions_can_wait_at_once()
    {
        var (s, _) = Create();
        for (var i = 0; i < 3; i++) Assert.Equal(201, (await s.SuggestAsync("me", Good(r => r.Title = $"Role {i}"))).Code);

        var fourth = await s.SuggestAsync("me", Good());

        Assert.Equal(400, fourth.Code);
        Assert.Contains("3 suggestions waiting", fourth.Message);
    }

    [Fact]
    public async Task Suggestions_waiting_at_another_institution_do_not_count_against_the_limit()
    {
        var (s, _) = Create(Posting("x", "Pending", tenant: Other, postedBy: "me"), Posting("y", "Pending", tenant: Other, postedBy: "me"), Posting("z", "Pending", tenant: Other, postedBy: "me"));

        Assert.Equal(201, (await s.SuggestAsync("me", Good())).Code);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hi")]
    [InlineData("mailto:a@b.com")]
    [InlineData("ftp://x.com/a")]
    [InlineData("not a url")]
    public async Task Only_web_links_are_accepted_for_applying(string link)
    {
        var (s, _) = Create();

        Assert.Equal(400, (await s.SuggestAsync("me", Good(r => r.ApplyUrl = link))).Code);
    }

    [Fact]
    public async Task Bad_input_is_refused_with_a_plain_reason()
    {
        var (s, _) = Create();

        Assert.Equal(400, (await s.SuggestAsync("me", Good(r => r.Title = "Hi"))).Code);
        Assert.Equal(400, (await s.SuggestAsync("me", Good(r => r.Company = ""))).Code);
        Assert.Equal(400, (await s.SuggestAsync("me", Good(r => r.Type = "Pyramid scheme"))).Code);
        Assert.Equal(400, (await s.SuggestAsync("me", Good(r => r.Deadline = DateTime.UtcNow.AddDays(-2)))).Code);
        Assert.Equal(400, (await s.SuggestAsync("me", Good(r => r.Description = new string('x', 4001)))).Code);
    }

    [Fact]
    public async Task The_type_is_matched_case_insensitively_and_stored_in_its_canonical_form()
    {
        var (s, _) = Create();

        Assert.Equal("Scholarship", (await s.SuggestAsync("me", Good(r => r.Type = "scholarship"))).Data!.Type);
    }
}
