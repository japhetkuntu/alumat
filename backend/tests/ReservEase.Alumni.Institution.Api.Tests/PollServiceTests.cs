using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Polls;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>The admin's side of an election: a draft is free to change, a published poll that nobody has voted in can still be taken back, and once anyone has voted the ballot is fixed.</summary>
public class PollServiceTests
{
    private const string Tenant = "inst-1";
    private static readonly AuthData Boss = new() { Id = "boss", Role = "SuperAdmin" };

    // A real relational database (SQLite), not the in-memory one: the service replaces a ballot with bulk deletes, and the database's own
    // unique and foreign-key rules are part of what keeps a vote safe, so they have to be there.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Microsoft.Data.Sqlite.SqliteConnection> Connections = new();

    private static (PollService Service, string Db) Create(Action<ReservEase.Alumni.PostgresDb.Sdk.DbContexts.AlumniDbContext>? seed = null)
    {
        var (db, connection) = TestDb.CreateRelational(Tenant);
        var name = TestDb.NewName();
        Connections[name] = connection;
        for (var i = 1; i <= 3; i++) db.Add(new MemberEntity { Id = $"m{i}", FirstName = $"M{i}", LastName = "X", Email = $"m{i}@x.org", GraduationYear = 2015, Status = "Active", InstitutionId = Tenant });
        seed?.Invoke(db); db.SaveChanges();
        db.Dispose();
        return (Open(name), name);
    }

    /// <summary>A service over a fresh context on the same database, so it sees what other contexts wrote rather than what it cached.</summary>
    private static PollService Open(string name) => Open(name, out _);

    private static PollService Open(string name, out ReservEase.Alumni.PostgresDb.Sdk.DbContexts.AlumniDbContext ctx)
    {
        ctx = TestDb.OpenRelational(Connections[name], Tenant);
        var engine = new PollEngine(new AlumniPgRepository<Poll>(ctx), new AlumniPgRepository<PollPosition>(ctx), new AlumniPgRepository<PollAspirant>(ctx),
            new AlumniPgRepository<PollParticipation>(ctx), new AlumniPgRepository<PollVote>(ctx), new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<CommunityMembership>(ctx));
        return new PollService(
            new AlumniPgRepository<Poll>(ctx), new AlumniPgRepository<PollPosition>(ctx), new AlumniPgRepository<PollAspirant>(ctx), new AlumniPgRepository<PollParticipation>(ctx),
            new AlumniPgRepository<Community>(ctx), new AlumniPgRepository<Notification>(ctx), engine, Mock.Of<IInstitutionAuditLogService>(), TestDb.Tenant(Tenant, "umat"), NullLogger<PollService>.Instance);
    }

    private static ReservEase.Alumni.PostgresDb.Sdk.DbContexts.AlumniDbContext Raw(string name) => TestDb.OpenRelational(Connections[name], Tenant);

    private static SavePollRequest Request(Action<SavePollRequest>? tweak = null)
    {
        var r = new SavePollRequest
        {
            Title = "Executive election", OpensAt = DateTime.UtcNow.AddHours(-1), ClosesAt = DateTime.UtcNow.AddDays(2), ResultsMode = "AfterClose",
            Positions =
            [
                new() { Title = "President", Aspirants = [new() { Name = "Ama" }, new() { Name = "Kofi" }] },
                new() { Title = "Treasurer", Aspirants = [new() { Name = "Esi" }] },
            ],
        };
        tweak?.Invoke(r);
        return r;
    }

    [Fact]
    public async Task A_draft_is_saved_with_its_ballot_and_one_aspirant_makes_a_yes_no_position()
    {
        var (service, _) = Create();
        var saved = (await service.CreatePollAsync(Request(), Boss)).Data!;

        Assert.Equal("Draft", saved.Status);
        Assert.Equal(["President", "Treasurer"], saved.Positions.Select(p => p.Title));
        Assert.False(saved.Positions[0].IsYesNo);
        Assert.True(saved.Positions[1].IsYesNo);
        Assert.Equal(3, saved.Eligible);
        Assert.True(saved.CanDelete);
    }

    [Fact]
    public async Task Bad_input_is_refused_with_a_reason()
    {
        var (service, _) = Create();
        Assert.Equal(400, (await service.CreatePollAsync(Request(r => r.Title = " "), Boss)).Code);
        Assert.Equal(400, (await service.CreatePollAsync(Request(r => r.ClosesAt = r.OpensAt), Boss)).Code);
        Assert.Equal(400, (await service.CreatePollAsync(Request(r => r.ResultsMode = "Whenever"), Boss)).Code);
        Assert.Equal(400, (await service.CreatePollAsync(Request(r => r.CommunityId = "ghost"), Boss)).Code);
        Assert.Equal(400, (await service.CreatePollAsync(Request(r => r.Positions![0].Aspirants[0].Name = ""), Boss)).Code);
    }

    [Fact]
    public async Task A_poll_cannot_be_published_until_every_position_has_an_aspirant_and_somebody_can_vote()
    {
        var (service, _) = Create();
        var empty = (await service.CreatePollAsync(Request(r => r.Positions = []), Boss)).Data!;
        Assert.Equal(400, (await service.PublishAsync(empty.Id, Boss)).Code);

        var hollow = (await service.CreatePollAsync(Request(r => r.Positions!.Add(new() { Title = "Secretary" })), Boss)).Data!;
        var refused = await service.PublishAsync(hollow.Id, Boss);
        Assert.Equal(400, refused.Code);
        Assert.Contains("Secretary", refused.Message);

        var nobody = (await service.CreatePollAsync(Request(r => r.YearGroups = [1999]), Boss)).Data!;
        Assert.Equal(400, (await service.PublishAsync(nobody.Id, Boss)).Code);

        var ok = (await service.CreatePollAsync(Request(), Boss)).Data!;
        Assert.Equal("Published", (await service.PublishAsync(ok.Id, Boss)).Data!.Status);
    }

    [Fact]
    public async Task Publishing_tells_everyone_who_can_vote_and_nobody_else()
    {
        var (service, name) = Create();
        var poll = (await service.CreatePollAsync(Request(r => r.YearGroups = [2015]), Boss)).Data!;
        await service.PublishAsync(poll.Id, Boss);

        using var db = Raw(name);
        var told = await db.Notifications.Where(n => n.Type == "PollOpen").Select(n => n.RecipientId).ToListAsync();
        Assert.Equal(["m1", "m2", "m3"], told.OrderBy(x => x));
    }

    [Fact]
    public async Task Once_anyone_has_voted_the_ballot_and_audience_are_fixed_and_the_poll_cannot_be_deleted_or_unpublished()
    {
        var (service, name) = Create();
        var poll = (await service.CreatePollAsync(Request(), Boss)).Data!;
        await service.PublishAsync(poll.Id, Boss);
        using (var db = Raw(name)) { db.Add(new PollParticipation { PollId = poll.Id, MemberId = "m1", InstitutionId = Tenant }); db.SaveChanges(); }

        Assert.Equal(400, (await service.UpdatePollAsync(poll.Id, Request(), Boss)).Code);                                            // sends a ballot
        Assert.Equal(400, (await service.UpdatePollAsync(poll.Id, Request(r => { r.Positions = null; r.YearGroups = [2015]; }), Boss)).Code);   // changes the audience
        Assert.Equal(400, (await service.DeleteAsync(poll.Id, Boss)).Code);
        Assert.Equal(400, (await service.UnpublishAsync(poll.Id, Boss)).Code);

        var later = DateTime.UtcNow.AddDays(5);
        var extended = await service.UpdatePollAsync(poll.Id, Request(r => { r.Positions = null; r.Title = "Executive election 2027"; r.ClosesAt = later; r.OpensAt = poll.OpensAt; }), Boss);
        Assert.Equal(200, extended.Code);
        Assert.Equal("Executive election 2027", extended.Data!.Title);
        Assert.True(extended.Data.Locked);
    }

    [Fact]
    public async Task A_poll_nobody_voted_in_can_go_back_to_a_draft_and_be_deleted()
    {
        var (service, name) = Create();
        var poll = (await service.CreatePollAsync(Request(), Boss)).Data!;
        await service.PublishAsync(poll.Id, Boss);

        Assert.Equal("Draft", (await service.UnpublishAsync(poll.Id, Boss)).Data!.Status);
        Assert.Equal(200, (await service.DeleteAsync(poll.Id, Boss)).Code);
        Assert.Equal(404, (await Open(name).GetPollAsync(poll.Id)).Code);       // a fresh request, as in production
    }

    [Fact]
    public async Task Only_a_closed_poll_can_be_archived_and_a_closed_poll_can_no_longer_be_edited()
    {
        var (service, name) = Create();
        var poll = (await service.CreatePollAsync(Request(), Boss)).Data!;
        await service.PublishAsync(poll.Id, Boss);
        Assert.Equal(400, (await service.SetArchivedAsync(poll.Id, true, Boss)).Code);          // still open

        using (var db = Raw(name)) db.Polls.ExecuteUpdate(u => u.SetProperty(p => p.OpensAt, DateTime.UtcNow.AddDays(-3)).SetProperty(p => p.ClosesAt, DateTime.UtcNow.AddMinutes(-5)));
        service = Open(name);

        Assert.Equal(400, (await service.UpdatePollAsync(poll.Id, Request(), Boss)).Code);
        var archived = (await service.SetArchivedAsync(poll.Id, true, Boss)).Data!;
        Assert.True(archived.IsArchived);
        Assert.Empty((await service.GetPollsAsync(archived: false)).Data!);
        Assert.Single((await service.GetPollsAsync(archived: true)).Data!);
    }

    [Fact]
    public async Task An_admin_cannot_see_the_figures_while_voting_is_open_unless_the_poll_is_live_and_results_publish_only_by_hand_after_closing()
    {
        var (service, name) = Create();
        var poll = (await service.CreatePollAsync(Request(r => r.ResultsMode = "Manual"), Boss)).Data!;
        await service.PublishAsync(poll.Id, Boss);

        var open = (await service.GetResultsAsync(poll.Id)).Data!;
        Assert.False(open.FiguresAvailable);
        Assert.Empty(open.Positions);
        Assert.Equal(400, (await service.PublishResultsAsync(poll.Id, Boss)).Code);          // not closed yet

        using (var db = Raw(name)) db.Polls.ExecuteUpdate(u => u.SetProperty(p => p.OpensAt, DateTime.UtcNow.AddDays(-3)).SetProperty(p => p.ClosesAt, DateTime.UtcNow.AddMinutes(-5)));
        service = Open(name);

        var closed = (await service.GetResultsAsync(poll.Id)).Data!;
        Assert.True(closed.FiguresAvailable);
        Assert.False(closed.ShownToMembers);
        Assert.True(closed.CanPublish);

        Assert.Equal(200, (await service.PublishResultsAsync(poll.Id, Boss)).Code);
        Assert.Equal(400, (await service.PublishResultsAsync(poll.Id, Boss)).Code);          // once
        Assert.True((await service.GetResultsAsync(poll.Id)).Data!.ShownToMembers);
        Assert.Equal(400, (await service.UpdatePollAsync(poll.Id, Request(), Boss)).Code);   // and it is closed anyway
    }

    [Fact]
    public async Task A_live_poll_shows_its_figures_to_the_admin_while_open_and_automatic_modes_reject_publishing_by_hand()
    {
        var (service, _) = Create();
        var live = (await service.CreatePollAsync(Request(r => r.ResultsMode = "Live"), Boss)).Data!;
        await service.PublishAsync(live.Id, Boss);
        var r = (await service.GetResultsAsync(live.Id)).Data!;
        Assert.True(r.FiguresAvailable);
        Assert.True(r.ShownToMembers);
        Assert.Equal(400, (await service.PublishResultsAsync(live.Id, Boss)).Code);
    }
}
