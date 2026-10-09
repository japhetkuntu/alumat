using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class OpportunitySuggestionServiceTests
{
    private const string Tenant = "inst-1";
    private const string Other = "inst-2";
    private static readonly AuthData Super = new() { Id = "admin", FirstName = "Ama", LastName = "Admin", Role = "SuperAdmin" };

    private static (OpportunitySuggestionService s, string db, Mock<IInstitutionAuditLogService> audit) Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant)) { foreach (var e in seed) db.Add(e); db.SaveChanges(); }
        var ctx = TestDb.Create(name, Tenant);
        var audit = new Mock<IInstitutionAuditLogService>();
        var tenant = TestDb.Tenant(Tenant);
        return (new OpportunitySuggestionService(new AlumniPgRepository<Job>(ctx), new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<Notification>(ctx),
            Mock.Of<ITemporalClientProvider>(t => t.IsAvailable == false), tenant, audit.Object, NullLogger<OpportunitySuggestionService>.Instance), name, audit);
    }

    private static MemberEntity Person(string id = "m1", string tenant = Tenant) => new() { Id = id, InstitutionId = tenant, FirstName = "Yaw", LastName = "Tester", Email = $"{id}@x.com", GraduationYear = 2014, Status = "Active" };
    private static Job Suggestion(string id = "j1", string by = "m1", string status = "Pending", string tenant = Tenant) =>
        new() { Id = id, InstitutionId = tenant, Title = "Internship", Company = "Acme", Location = "Accra", Type = "Internship", Status = status, PostedBy = by, CreatedBy = by };

    [Fact]
    public async Task Approving_makes_it_live_tells_the_member_and_is_audited()
    {
        var (s, db, audit) = Create(Person(), Suggestion());

        var r = await s.ApproveAsync("j1", Super);

        Assert.Equal(("Active", 200), (r.Data!.Status, (int)r.Code));
        using var check = TestDb.Create(db, Tenant);
        var note = Assert.Single(check.Set<Notification>().ToList());
        Assert.Equal(("m1", "Your suggestion is live", "/jobs/j1"), (note.RecipientId, note.Title, note.ActionUrl));
        audit.Verify(a => a.LogAsync(Super, "Opportunity Approved", "Internship"), Times.Once);
    }

    [Fact]
    public async Task Declining_closes_it_without_deleting_and_tells_the_member_kindly()
    {
        var (s, db, _) = Create(Person(), Suggestion());

        Assert.Equal(200, (await s.DeclineAsync("j1", Super)).Code);

        using var check = TestDb.Create(db, Tenant);
        Assert.Equal("Closed", check.Set<Job>().Single().Status);
        Assert.Contains("not able to share", check.Set<Notification>().Single().Body);
    }

    [Theory]
    [InlineData("Active")] [InlineData("Closed")] [InlineData("Draft")]
    public async Task Only_a_pending_suggestion_can_be_reviewed(string status)
    {
        var (s, _, _) = Create(Person(), Suggestion(status: status));

        Assert.Equal(400, (await s.ApproveAsync("j1", Super)).Code);
        Assert.Equal(400, (await s.DeclineAsync("j1", Super)).Code);
    }

    [Fact]
    public async Task Approving_twice_does_not_alert_or_notify_twice()
    {
        var (s, db, _) = Create(Person(), Suggestion());

        await s.ApproveAsync("j1", Super);
        var second = await s.ApproveAsync("j1", Super);

        Assert.Equal(400, second.Code);
        using var check = TestDb.Create(db, Tenant);
        Assert.Single(check.Set<Notification>().ToList());
    }

    [Fact]
    public async Task Another_institutions_suggestion_cannot_be_reviewed_and_looks_like_it_does_not_exist()
    {
        var (s, _, _) = Create(Person("x", Other), Suggestion("foreign", "x", tenant: Other));

        Assert.Equal(404, (await s.ApproveAsync("foreign", Super)).Code);
        Assert.Equal(404, (await s.DeclineAsync("foreign", Super)).Code);
    }

    [Fact]
    public async Task A_scoped_administrator_cannot_review_an_institution_wide_suggestion()
    {
        var (s, _, _) = Create(Person(), Suggestion());
        var scoped = new AuthData { Id = "amb", Role = "ScopedAdmin", YearGroups = [2015] };

        Assert.Equal(404, (await s.ApproveAsync("j1", scoped)).Code);
    }

    [Fact]
    public async Task A_suggestion_whose_sender_is_no_longer_a_member_is_still_reviewable_without_a_notification()
    {
        var (s, db, _) = Create(Suggestion(by: "gone"));

        Assert.Equal(200, (await s.ApproveAsync("j1", Super)).Code);
        using var check = TestDb.Create(db, Tenant);
        Assert.Empty(check.Set<Notification>().ToList());
    }

    [Fact]
    public async Task The_list_names_who_suggested_each_pending_item_and_leaves_live_ones_alone()
    {
        var (s, _, _) = Create(Person(), Suggestion("p"), Suggestion("live", by: "staff", status: "Active"));
        var jobs = new[] { new JobDto { Id = "p", Status = "Pending" }, new JobDto { Id = "live", Status = "Active" } };

        await s.NameSuggestersAsync(jobs);

        Assert.Equal("Yaw Tester (class of 2014)", jobs[0].SuggestedByName);
        Assert.Null(jobs[1].SuggestedByName);
    }
}
