using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Operations.Worker.Workflows.Reports;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Reports.Sdk.Models;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using Temporalio.Client;
using Temporalio.Worker;

namespace ReservEase.Alumni.Operations.Worker.Tests;

[Collection("Temporal")]
public class GenerateReportWorkflowTests(TemporalFixture temporal)
{
    private const string Inst = "inst-1";

    private sealed class Rig : IDisposable
    {
        private readonly Microsoft.Data.Sqlite.SqliteConnection connection;
        public InMemoryPrivateStorage Storage { get; } = new();
        public ReportActivities Activities { get; }

        public Rig()
        {
            var (db, conn) = TestDb.CreateRelational();
            connection = conn;
            db.Dispose();
            var ctx = TestDb.OpenRelational(connection);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminPortalBaseDomain"] = "admin.example.test",
                ["PlatformPortalUrl"] = "https://platform.example.test/",
            }).Build();
            Activities = new ReportActivities(
                new AlumniPgRepository<ReportJob>(ctx), new AlumniPgRepository<Institution>(ctx), new AlumniPgRepository<Notification>(ctx),
                new AlumniPgRepository<PlatformNotification>(ctx), ReportRig.Builder(ctx), Storage,
                // Email goes out through the notification workflow; with no Temporal here it is dropped, which is all these tests need.
                Mock.Of<ITemporalClientProvider>(p => p.IsAvailable == false), configuration, NullLogger<ReportActivities>.Instance);
        }

        public AlumniDbContext Db() => TestDb.OpenRelational(connection);

        public async Task Seed(params object[] entities)
        {
            using var db = Db();
            db.AddRange(entities);
            await db.SaveChangesAsync();
        }

        public async Task<ReportJob> Job(string id)
        {
            using var db = Db();
            return await db.ReportJobs.SingleAsync(j => j.Id == id);
        }

        public void Dispose() => connection.Dispose();
    }

    private async Task Run(Rig rig, string reportJobId)
    {
        var queue = "q-" + Guid.NewGuid().ToString("N");
        using var worker = new TemporalWorker(temporal.Client,
            new TemporalWorkerOptions(queue).AddWorkflow<GenerateReportWorkflow>().AddAllActivities(rig.Activities));
        await worker.ExecuteAsync(() => temporal.Client.ExecuteWorkflowAsync(
            (GenerateReportWorkflow wf) => wf.RunAsync(reportJobId),
            new WorkflowOptions("wf-" + Guid.NewGuid().ToString("N"), queue)));
    }

    private static ReportJob Request(string id = "job-1", string type = ReportTypes.Members, string format = "csv") => new()
    {
        Id = id, Audience = ReportAudiences.Institution, InstitutionId = Inst, RequestedById = "admin-1", RequestedByName = "Ama Mensah",
        RequestedByEmail = "ama@x.com", ReportType = type, Title = "Member roster", Format = format,
    };

    private static Member Person(string id, string institution = Inst) => new()
    {
        Id = id, InstitutionId = institution, FirstName = id, LastName = "Mensah", Email = $"{id}@x.com", GraduationYear = 2014, Status = "Active", DepartmentId = "d",
    };

    [WorkflowFact]
    public async Task A_requested_report_ends_up_ready_with_its_file_stored_privately_and_the_requester_told()
    {
        using var rig = new Rig();
        await rig.Seed(new Institution { Id = Inst, Name = "UMaT Alumni", Slug = "umat" }, Request(), Person("ama"), Person("kofi"), Person("stranger", "inst-2"));

        await Run(rig, "job-1");

        var job = await rig.Job("job-1");
        Assert.Equal((ReportJobStatuses.Ready, 2), (job.Status, job.RowCount));
        Assert.NotNull(job.StartedAt);
        Assert.NotNull(job.CompletedAt);
        Assert.Null(job.ExpiresAt);   // finished reports never expire
        Assert.StartsWith("member-roster-", job.FileName);
        Assert.EndsWith(".csv", job.FileName);

        var (content, contentType) = Assert.Single(rig.Storage.Files, f => f.Key == job.FileKey).Value;
        Assert.Equal("text/csv", contentType);
        Assert.Equal(content.Length, job.FileSizeBytes);
        var text = Encoding.UTF8.GetString(content);
        Assert.Contains("ama@x.com", text);
        Assert.DoesNotContain("stranger", text);
        // Filed under its institution and job, so no two reports can ever share a key.
        Assert.StartsWith($"reports/{Inst}/job-1/", job.FileKey);

        using var db = rig.Db();
        var note = await db.Notifications.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("admin-1", "Admin", "ReportReady", Inst), (note.RecipientId, note.RecipientType, note.Type, note.InstitutionId));
        Assert.Equal("https://umat.admin.example.test/reports", note.ActionUrl);
        Assert.Contains("2 rows", note.Body);
    }

    [WorkflowFact]
    public async Task An_excel_report_is_stored_as_a_spreadsheet()
    {
        using var rig = new Rig();
        await rig.Seed(Request(format: "xlsx"), Person("ama"));

        await Run(rig, "job-1");

        var job = await rig.Job("job-1");
        Assert.EndsWith(".xlsx", job.FileName);
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(rig.Storage.Files[job.FileKey!].Content));
        Assert.Equal("ama", workbook.Worksheets.Single().Cell(2, 2).GetString());
    }

    // Called directly rather than through the workflow: every platform report sums money in the database,
    // which the SQLite test database can't do for decimals. The reports themselves are covered on the
    // in-memory provider in ReportDataBuilderTests.
    [Fact]
    public async Task A_finished_platform_report_notifies_the_staff_member_who_asked_and_no_institution()
    {
        using var rig = new Rig();
        var request = Request(type: ReportTypes.PlatformInstitutions);
        request.Audience = ReportAudiences.Platform;
        request.InstitutionId = null;
        request.RequestedById = "staff-1";
        request.Status = ReportJobStatuses.Ready;
        request.RowCount = 1;
        await rig.Seed(request);

        await rig.Activities.NotifyRequesterAsync("job-1");

        using var db = rig.Db();
        var note = await db.PlatformNotifications.SingleAsync();
        Assert.Equal(("staff-1", "ReportReady", "/reports"), (note.RecipientStaffId, note.Type, note.ActionUrl));
        Assert.Contains("1 row.", note.Body);
        Assert.Empty(await db.Notifications.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Nobody_is_notified_about_a_report_that_has_not_finished()
    {
        using var rig = new Rig();
        await rig.Seed(Request());

        await rig.Activities.NotifyRequesterAsync("job-1");

        using var db = rig.Db();
        Assert.Empty(await db.Notifications.IgnoreQueryFilters().ToListAsync());
    }

    [WorkflowFact]
    public async Task A_report_that_cannot_be_built_is_marked_failed_and_the_requester_is_told_why()
    {
        using var rig = new Rig();
        await rig.Seed(Request(type: "no-such-report"));

        await Run(rig, "job-1");

        var job = await rig.Job("job-1");
        Assert.Equal(ReportJobStatuses.Failed, job.Status);
        Assert.NotNull(job.CompletedAt);
        Assert.False(string.IsNullOrEmpty(job.FailureReason));
        Assert.Null(job.FileKey);
        Assert.Empty(rig.Storage.Files);
        using var db = rig.Db();
        Assert.Equal("ReportFailed", (await db.Notifications.IgnoreQueryFilters().SingleAsync()).Type);
    }

    [WorkflowFact]
    public async Task A_request_for_a_job_that_does_not_exist_finishes_quietly()
    {
        using var rig = new Rig();

        await Run(rig, "never-recorded");

        Assert.Empty(rig.Storage.Files);
        using var db = rig.Db();
        Assert.Empty(await db.Notifications.IgnoreQueryFilters().ToListAsync());
    }

    // ── Clean-up ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Clean_up_never_deletes_or_expires_a_finished_report_however_old()
    {
        using var rig = new Rig();
        ReportJob Ready(string id, double daysOld) { var j = Request(id); j.Status = ReportJobStatuses.Ready; j.FileKey = $"reports/{id}.csv"; j.CreatedAt = DateTime.UtcNow.AddDays(-daysOld); j.ExpiresAt = null; return j; }
        await rig.Seed(Ready("ancient", 400), Ready("fresh", 1));
        // A report that still carries an old expiry date from before reports stopped expiring.
        var legacy = Ready("legacy", 30); legacy.ExpiresAt = DateTime.UtcNow.AddDays(-23);
        await rig.Seed(legacy);
        foreach (var id in new[] { "ancient", "fresh", "legacy" }) rig.Storage.Files[$"reports/{id}.csv"] = ([1], "text/csv");

        var touched = await rig.Activities.ExpireOldReportsAsync();

        Assert.Equal(0, touched);
        Assert.Equal(3, rig.Storage.Files.Count);
        foreach (var id in new[] { "ancient", "fresh", "legacy" })
        {
            var job = await rig.Job(id);
            Assert.Equal((ReportJobStatuses.Ready, true), (job.Status, job.FileKey is not null));
        }
    }

    [Fact]
    public async Task Clean_up_fails_a_job_left_unfinished_for_hours_so_it_can_be_requested_again()
    {
        using var rig = new Rig();
        var stuck = Request("stuck"); stuck.Status = ReportJobStatuses.Running; stuck.CreatedAt = DateTime.UtcNow.AddHours(-7);
        var inProgress = Request("in-progress"); inProgress.Status = ReportJobStatuses.Running; inProgress.CreatedAt = DateTime.UtcNow.AddMinutes(-5);
        await rig.Seed(stuck, inProgress);

        await rig.Activities.ExpireOldReportsAsync();

        Assert.Equal(ReportJobStatuses.Failed, (await rig.Job("stuck")).Status);
        Assert.Equal(ReportJobStatuses.Running, (await rig.Job("in-progress")).Status);
    }
}
