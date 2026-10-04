using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Filters;
using ReservEase.Alumni.PostgresDb.Sdk.Marketing;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.PostgresDb.Tests;

public class MembershipActivityCalculatorTests
{
    [Theory]
    [InlineData("DuesRequired", "Active", true, true)]
    [InlineData("DuesRequired", "Active", false, false)]
    [InlineData("DuesRequired", "Pending", true, true)]
    [InlineData(null, "Active", false, false)]
    [InlineData(null, "Active", true, true)]
    [InlineData("SomethingElse", "Active", false, false)]
    [InlineData("ApprovedOnly", "Active", false, true)]
    [InlineData("ApprovedOnly", "Active", true, true)]
    [InlineData("ApprovedOnly", "Pending", true, false)]
    [InlineData("ApprovedOnly", "Suspended", true, false)]
    [InlineData("ApprovedOnly", null, true, false)]
    public void ResolveActive_follows_the_policy_table(string? policy, string? status, bool duesMet, bool expected)
        => Assert.Equal(expected, MembershipActivityCalculator.ResolveActive(policy, status, duesMet));

    [Fact]
    public void Policy_constants_are_stable_because_they_are_stored()
    {
        Assert.Equal("DuesRequired", MembershipActivityCalculator.DuesRequiredPolicy);
        Assert.Equal("ApprovedOnly", MembershipActivityCalculator.ApprovedOnlyPolicy);
    }
}

public class CurrentTenantServiceTests
{
    [Fact]
    public void Starts_unset()
    {
        var tenant = new CurrentTenantService();
        Assert.Null(tenant.InstitutionId);
        Assert.Null(tenant.InstitutionSlug);
    }

    [Fact]
    public void SetInstitutionId_records_id_and_slug()
    {
        var tenant = new CurrentTenantService();
        tenant.SetInstitutionId("i1", "umat");
        Assert.Equal(("i1", "umat"), (tenant.InstitutionId, tenant.InstitutionSlug));
    }

    [Fact]
    public void Slug_is_optional_and_replaced_on_every_set()
    {
        var tenant = new CurrentTenantService();
        tenant.SetInstitutionId("i1", "umat");
        tenant.SetInstitutionId("i2");
        Assert.Equal("i2", tenant.InstitutionId);
        Assert.Null(tenant.InstitutionSlug);
    }
}

public class MarketingRulesTests
{
    [Theory]
    [InlineData("/", true)]
    [InlineData("/#onboard", true)]
    [InlineData("/#features", true)]
    [InlineData("/#product", true)]
    [InlineData("/#costs", true)]
    [InlineData("/why-not-whatsapp", true)]
    [InlineData("/login", false)]
    [InlineData("https://evil.example", false)]
    [InlineData("//evil.example", false)]
    [InlineData("", false)]
    [InlineData("/#onboard ", false)]
    [InlineData("/WHY-NOT-WHATSAPP", false)]
    public void Destinations_are_an_allow_list_never_an_arbitrary_redirect(string path, bool expected)
        => Assert.Equal(expected, MarketingRules.ValidDestination(path));

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("https://facebook.com/post/1", true)]
    [InlineData("http://facebook.com/post/1", false)]
    [InlineData("ftp://x.com", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("not a url", false)]
    [InlineData("https://user:pass@x.com", false)]
    [InlineData("/relative", false)]
    public void Published_urls_must_be_blank_or_credential_free_https(string? url, bool expected)
        => Assert.Equal(expected, MarketingRules.ValidPublishedUrl(url));

    [Fact]
    public void Channels_and_statuses_are_the_documented_sets()
    {
        Assert.Contains("WhatsApp", MarketingRules.Channels);
        Assert.Contains("Other", MarketingRules.Channels);
        Assert.Equal(new[] { "Draft", "Ready", "Archived" }, MarketingRules.Statuses);
        Assert.Equal(MarketingRules.Channels.Length, MarketingRules.Channels.Distinct().Count());
    }

    [Theory]
    [InlineData(1)] [InlineData(7)] [InlineData(32)]
    public void Short_codes_have_the_requested_length(int length)
        => Assert.Equal(length, MarketingRules.GenerateShortCode(length).Length);

    [Fact]
    public void Short_codes_default_to_seven_characters_and_avoid_ambiguous_glyphs()
    {
        var codes = Enumerable.Range(0, 500).Select(_ => MarketingRules.GenerateShortCode()).ToList();
        Assert.All(codes, c =>
        {
            Assert.Equal(7, c.Length);
            Assert.DoesNotContain(c, ch => "01OlI".Contains(ch));
            Assert.All(c, ch => Assert.True(char.IsLetterOrDigit(ch)));
        });
        Assert.True(codes.Distinct().Count() > 490); // 55^7 combinations, collisions are vanishingly rare
    }

    [Fact]
    public void Snapshot_deserialises_the_stored_json()
    {
        var share = new MarketingShare
        {
            SnapshotJson = "{\"CampaignTitle\":\"C\",\"Title\":\"T\",\"Content\":\"Body\",\"DestinationPath\":\"/\",\"UseLandingPage\":true,\"Assets\":[]}",
        };
        var snapshot = MarketingRules.Snapshot(share);
        Assert.Equal(("C", "T", "Body", "/", true), (snapshot.CampaignTitle, snapshot.Title, snapshot.Content, snapshot.DestinationPath, snapshot.UseLandingPage));
        Assert.Empty(snapshot.Assets);
    }
}

public class RequireFeatureAttributeTests
{
    private static ActionExecutingContext Context(Institution? institution)
    {
        var http = new DefaultHttpContext();
        if (institution is not null) http.Items["Institution"] = institution;
        return new ActionExecutingContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()), new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
    }

    private static async Task<(bool nextCalled, ActionExecutingContext ctx)> Run(string feature, Institution? institution)
    {
        var ctx = Context(institution);
        var called = false;
        await new RequireFeatureAttribute(feature).OnActionExecutionAsync(ctx, () =>
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(ctx, new List<IFilterMetadata>(), new object()));
        });
        return (called, ctx);
    }

    [Fact]
    public async Task A_disabled_feature_is_blocked_with_403_and_never_reaches_the_action()
    {
        var (called, ctx) = await Run("Store", new Institution { DisabledFeatures = ["Store"] });

        Assert.False(called);
        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(403, result.StatusCode);
        var body = Assert.IsType<ApiResponse<object>>(result.Value);
        Assert.Equal(403, body.Code);
        Assert.Contains("Store", body.Message);
        Assert.Contains("not enabled", body.Message);
    }

    [Fact]
    public async Task An_enabled_feature_passes_through()
    {
        var (called, ctx) = await Run("Store", new Institution { DisabledFeatures = ["Jobs"] });
        Assert.True(called);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task No_resolved_institution_passes_through_because_tenant_resolution_decides_that()
    {
        var (called, ctx) = await Run("Store", null);
        Assert.True(called);
        Assert.Null(ctx.Result);
    }

    [Fact]
    public async Task Feature_names_are_matched_exactly()
    {
        var (called, _) = await Run("Store", new Institution { DisabledFeatures = ["store"] });
        Assert.True(called);
    }
}

public class StaffActivityRecorderTests
{
    private static readonly DateTime Wednesday = new(2026, 10, 7, 15, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime MondayOfThatWeek = new(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);

    private static (StaffActivityRecorder recorder, string db) Create(string? db = null)
    {
        db ??= TestDb.NewName();
        var ctx = TestDb.Create(db);
        return (new StaffActivityRecorder(new AlumniPgRepository<StaffActivityWeek>(ctx), Mock.Of<ILogger<StaffActivityRecorder>>()), db);
    }

    [Fact]
    public async Task Records_one_row_for_the_monday_of_the_week()
    {
        var (recorder, db) = Create();
        await recorder.RecordAsync("i1", "s1", Wednesday);

        var row = Assert.Single(await TestDb.Create(db).StaffActivityWeeks.ToListAsync());
        Assert.Equal(("i1", "s1", MondayOfThatWeek, "s1"), (row.InstitutionId, row.StaffId, row.WeekStart, row.CreatedBy));
    }

    [Fact]
    public async Task Is_idempotent_within_a_week()
    {
        var (recorder, db) = Create();
        await recorder.RecordAsync("i1", "s1", Wednesday);
        await recorder.RecordAsync("i1", "s1", Wednesday.AddDays(2));
        await recorder.RecordAsync("i1", "s1", MondayOfThatWeek);

        Assert.Equal(1, await TestDb.Create(db).StaffActivityWeeks.CountAsync());
    }

    [Fact]
    public async Task A_new_week_a_new_staff_member_or_a_new_institution_each_get_their_own_row()
    {
        var (recorder, db) = Create();
        await recorder.RecordAsync("i1", "s1", Wednesday);
        await recorder.RecordAsync("i1", "s1", Wednesday.AddDays(7));
        await recorder.RecordAsync("i1", "s2", Wednesday);
        await recorder.RecordAsync("i2", "s1", Wednesday);

        Assert.Equal(4, await TestDb.Create(db).StaffActivityWeeks.CountAsync());
    }

    [Fact]
    public async Task Never_throws_even_if_the_repository_fails()
    {
        var repo = new Mock<IAlumniPgRepository<StaffActivityWeek>>();
        repo.Setup(r => r.GetQueryable(It.IsAny<System.Linq.Expressions.Expression<Func<StaffActivityWeek, bool>>>(), It.IsAny<bool>()))
            .Throws(new InvalidOperationException("db down"));
        var recorder = new StaffActivityRecorder(repo.Object, Mock.Of<ILogger<StaffActivityRecorder>>());

        await recorder.RecordAsync("i1", "s1", Wednesday); // must not throw
    }
}

public class PgPagedResultTests
{
    [Fact]
    public void Defaults_to_an_empty_result_set()
    {
        var page = new PgPagedResult<string>();
        Assert.Empty(page.Results);
        Assert.Equal(0, page.TotalCount);
    }
}
