using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Reports.Sdk.Models;
using ReservEase.Alumni.Reports.Sdk.Services;
using ReservEase.Alumni.Reports.Sdk.Workflows;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using Temporalio.Client;

namespace ReservEase.Alumni.Operations.Worker.Tests;

[Collection("Temporal")]
public class ReportJobServiceTests(TemporalFixture temporal)
{
    private const string Inst = "inst-1";

    private static ReportRequester Admin(string id = "admin-1", bool scoped = false, string institution = Inst, params string[] disabled) => new(
        ReportAudiences.Institution, institution, id, "Ama Mensah", $"{id}@x.com", scoped ? "ScopedAdmin" : "SuperAdmin",
        scoped, scoped ? [2014] : [], scoped ? ["c1"] : [], disabled);

    private static ReportRequester Staff(string role, string id = "staff-1") => new(
        ReportAudiences.Platform, null, id, "Platform Person", $"{id}@x.com", role, false, [], [], []);

    private sealed record Rig(ReportJobService Service, InMemoryPrivateStorage Storage, string DbName)
    {
        public async Task<List<ReportJob>> Jobs()
        {
            using var db = TestDb.Create(DbName);
            return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(db.ReportJobs);
        }

        public async Task Seed(params ReportJob[] jobs)
        {
            using var db = TestDb.Create(DbName);
            db.AddRange(jobs);
            await db.SaveChangesAsync();
        }
    }

    private static Rig Create(ITemporalClientProvider provider)
    {
        var name = TestDb.NewName();
        var storage = new InMemoryPrivateStorage();
        return new Rig(new ReportJobService(new AlumniPgRepository<ReportJob>(TestDb.Create(name)), provider, storage, NullLogger<ReportJobService>.Instance), storage, name);
    }

    private static ITemporalClientProvider Unavailable() => Mock.Of<ITemporalClientProvider>(p => p.IsAvailable == false);
    private static ITemporalClientProvider Connected(ITemporalClient client) => Mock.Of<ITemporalClientProvider>(p => p.IsAvailable == true && p.Client == client);
    /// <summary>Claims to be connected, but any attempt to start a workflow throws.</summary>
    private static ITemporalClientProvider Broken() => Connected(new Mock<ITemporalClient>(MockBehavior.Strict).Object);

    private static List<string> Keys(Rig rig, ReportRequester requester) => rig.Service.GetAvailable(requester).Select(d => d.Key).ToList();

    // ── What may be requested ───────────────────────────────────────────────

    [Fact]
    public void An_institution_admin_is_offered_institution_reports_only()
    {
        var keys = Keys(Create(Unavailable()), Admin());

        Assert.Contains(ReportTypes.Members, keys);
        Assert.Contains(ReportTypes.StoreOrders, keys);
        Assert.DoesNotContain(keys, k => k.StartsWith("platform-"));
    }

    [Fact]
    public void A_report_for_a_switched_off_feature_is_not_offered()
    {
        var keys = Keys(Create(Unavailable()), Admin(disabled: [InstitutionFeatures.Contributions, InstitutionFeatures.Store]));

        Assert.Contains(ReportTypes.Members, keys);
        Assert.DoesNotContain(ReportTypes.Payments, keys);
        Assert.DoesNotContain(ReportTypes.DuesStanding, keys);
        Assert.DoesNotContain(ReportTypes.StoreOrders, keys);
    }

    [Fact]
    public void A_scoped_admin_is_not_offered_reports_that_cannot_be_cut_to_their_scope()
    {
        var keys = Keys(Create(Unavailable()), Admin(scoped: true));

        Assert.Contains(ReportTypes.Members, keys);
        Assert.DoesNotContain(ReportTypes.StoreOrders, keys);
        Assert.DoesNotContain(ReportTypes.ServiceRequests, keys);
    }

    [Theory]
    [InlineData("SuperAdmin", true)]
    [InlineData("Billing", true)]
    [InlineData("Support", false)]
    [InlineData("Sales", false)]
    public void Platform_money_reports_are_for_finance_roles(string role, bool allowed)
    {
        var keys = Keys(Create(Unavailable()), Staff(role));

        Assert.Contains(ReportTypes.PlatformInstitutions, keys);
        Assert.Equal(allowed, keys.Contains(ReportTypes.PlatformPayments));
        Assert.Equal(allowed, keys.Contains(ReportTypes.PlatformRevenue));
        Assert.DoesNotContain(ReportTypes.Members, keys);
    }

    [Theory]
    [InlineData("no-such-report")]
    [InlineData(ReportTypes.PlatformPayments)] // exists, but not for an institution admin
    public async Task Requesting_a_report_you_may_not_run_is_not_found_and_records_nothing(string type)
    {
        var rig = Create(Unavailable());

        var result = await rig.Service.RequestAsync(Admin(), new RequestReportRequest { ReportType = type });

        Assert.Equal(404, result.Code);
        Assert.Empty(await rig.Jobs());
    }

    // ── Validation ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ReportParameters.From, "10/03/2026")]
    [InlineData(ReportParameters.Status, "Whatever")]
    public async Task A_bad_filter_is_refused_with_a_reason(string key, string value)
    {
        var rig = Create(Unavailable());

        var result = await rig.Service.RequestAsync(Admin(), new RequestReportRequest { ReportType = ReportTypes.Payments, Parameters = new() { [key] = value } });

        Assert.Equal(400, result.Code);
        Assert.Empty(await rig.Jobs());
    }

    [Fact]
    public async Task A_date_range_that_ends_before_it_starts_is_refused()
    {
        var result = await Create(Unavailable()).Service.RequestAsync(Admin(), new RequestReportRequest
        {
            ReportType = ReportTypes.Payments, Parameters = new() { [ReportParameters.From] = "2026-03-10", [ReportParameters.To] = "2026-03-01" },
        });

        Assert.Equal(400, result.Code);
    }

    [Fact]
    public async Task An_unknown_format_is_refused()
        => Assert.Equal(400, (await Create(Unavailable()).Service.RequestAsync(Admin(), new RequestReportRequest { ReportType = ReportTypes.Members, Format = "pdf" })).Code);

    [Fact]
    public async Task With_the_worker_unreachable_the_request_fails_cleanly_and_leaves_no_job_behind()
    {
        var rig = Create(Unavailable());

        var result = await rig.Service.RequestAsync(Admin(), new RequestReportRequest { ReportType = ReportTypes.Members });

        Assert.Equal(500, result.Code);
        Assert.Empty(await rig.Jobs());
    }

    [Fact]
    public async Task If_the_workflow_cannot_be_started_the_job_is_marked_failed_not_left_queued()
    {
        var rig = Create(Broken());

        var result = await rig.Service.RequestAsync(Admin(), new RequestReportRequest { ReportType = ReportTypes.Members });

        Assert.Equal(500, result.Code);
        var job = Assert.Single(await rig.Jobs());
        Assert.Equal(ReportJobStatuses.Failed, job.Status);
        Assert.NotNull(job.FailureReason);
    }

    [Fact]
    public async Task Someone_with_three_reports_in_progress_is_asked_to_wait()
    {
        var rig = Create(Broken());
        await rig.Seed(
            new ReportJob { RequestedById = "admin-1", Status = ReportJobStatuses.Queued },
            new ReportJob { RequestedById = "admin-1", Status = ReportJobStatuses.Running },
            new ReportJob { RequestedById = "admin-1", Status = ReportJobStatuses.Running },
            new ReportJob { RequestedById = "admin-1", Status = ReportJobStatuses.Ready });

        Assert.Equal(409, (await rig.Service.RequestAsync(Admin(), new RequestReportRequest { ReportType = ReportTypes.Members })).Code);
        // Someone else's queue is their own.
        Assert.NotEqual(409, (await rig.Service.RequestAsync(Admin("admin-2"), new RequestReportRequest { ReportType = ReportTypes.Members })).Code);
    }

    // ── A successful request ────────────────────────────────────────────────

    [WorkflowFact]
    public async Task A_request_is_recorded_with_the_requesters_scope_and_a_workflow_is_started_for_it()
    {
        var rig = Create(Connected(temporal.Client));

        var result = await rig.Service.RequestAsync(Admin(scoped: true), new RequestReportRequest
        {
            ReportType = ReportTypes.Members, Format = "CSV",
            Parameters = new() { [ReportParameters.Location] = "  Kumasi ", [ReportParameters.From] = "2026-01-01", ["made-up"] = "x" },
        });

        Assert.Equal(201, result.Code);
        var job = Assert.Single(await rig.Jobs());
        Assert.Equal((ReportJobStatuses.Queued, "csv", "Member roster", Inst), (job.Status, job.Format, job.Title, job.InstitutionId));
        Assert.True(job.RequesterIsScoped);
        Assert.Equal([2014], job.ScopeYearGroups);
        Assert.Equal(["c1"], job.ScopeCommunityIds);
        // Trimmed; and filters the member roster doesn't take (a date range, an invented key) are dropped, not stored.
        Assert.Equal(new Dictionary<string, string> { [ReportParameters.Location] = "Kumasi" }, job.Parameters);

        var description = await temporal.Client.GetWorkflowHandle(ReportWorkflowId.For(job.Id)).DescribeAsync();
        Assert.Equal(ReportTaskQueues.Generation, description.TaskQueue);
    }

    // ── Listing and download ────────────────────────────────────────────────

    private static ReportJob Finished(string id, string requestedBy = "admin-1", string? institution = Inst, string audience = ReportAudiences.Institution,
        string status = ReportJobStatuses.Ready, DateTime? expires = null) => new()
    {
        Id = id, RequestedById = requestedBy, InstitutionId = institution, Audience = audience, Status = status,
        Title = "Member roster", ReportType = ReportTypes.Members, Format = "csv",
        FileKey = $"reports/{id}.csv", FileName = "member-roster.csv", ExpiresAt = expires ?? DateTime.UtcNow.AddDays(3),
    };

    [Fact]
    public async Task I_only_ever_see_my_own_reports_newest_first()
    {
        var rig = Create(Unavailable());
        var older = Finished("older"); older.CreatedAt = DateTime.UtcNow.AddHours(-2);
        await rig.Seed(older, Finished("newer"), Finished("a-colleagues", requestedBy: "admin-2"));

        var mine = (await rig.Service.ListAsync(Admin(), 1, 20)).Data!.Results.Select(j => j.Id);

        Assert.Equal(["newer", "older"], mine);
    }

    [Fact]
    public async Task I_can_download_my_own_finished_report()
    {
        var rig = Create(Unavailable());
        await rig.Seed(Finished("mine"));
        rig.Storage.Files["reports/mine.csv"] = ("a,b"u8.ToArray(), "text/csv");

        using var download = (await rig.Service.OpenDownloadAsync(Admin(), "mine"))!.Content;

        Assert.Equal("a,b", new StreamReader(download).ReadToEnd());
    }

    [Fact]
    public async Task Nobody_else_can_download_it_even_knowing_its_id()
    {
        var rig = Create(Unavailable());
        await rig.Seed(Finished("mine"));
        rig.Storage.Files["reports/mine.csv"] = ("a,b"u8.ToArray(), "text/csv");

        Assert.Null(await rig.Service.OpenDownloadAsync(Admin("admin-2"), "mine"));
        Assert.Null(await rig.Service.OpenDownloadAsync(Admin("admin-1", institution: "inst-2"), "mine"));
        Assert.Null(await rig.Service.OpenDownloadAsync(Staff("SuperAdmin", id: "admin-1"), "mine"));
    }

    [Fact]
    public async Task A_report_that_is_not_ready_cannot_be_downloaded()
    {
        var rig = Create(Unavailable());
        await rig.Seed(Finished("running", status: ReportJobStatuses.Running), Finished("failed", status: ReportJobStatuses.Failed));

        foreach (var id in new[] { "running", "failed", "no-such-id" })
            Assert.Null(await rig.Service.OpenDownloadAsync(Admin(), id));
    }

    [Fact]
    public async Task Finished_reports_never_expire_so_even_an_old_expiry_date_does_not_block_the_download()
    {
        var rig = Create(Unavailable());
        await rig.Seed(Finished("old", expires: DateTime.UtcNow.AddDays(-30)), Finished("none"));
        rig.Storage.Files["reports/old.csv"] = ("a,b"u8.ToArray(), "text/csv");
        rig.Storage.Files["reports/none.csv"] = ("c,d"u8.ToArray(), "text/csv");

        Assert.NotNull(await rig.Service.OpenDownloadAsync(Admin(), "old"));
        Assert.NotNull(await rig.Service.OpenDownloadAsync(Admin(), "none"));
    }
}
