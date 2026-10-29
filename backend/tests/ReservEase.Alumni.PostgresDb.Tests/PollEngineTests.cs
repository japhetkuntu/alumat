using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Polls;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.PostgresDb.Tests;

/// <summary>Casting a ballot and counting it: one vote each, only for the right people, only while open, and nothing ties a choice to a person.</summary>
public class PollEngineTests
{
    private const string Tenant = "inst-1";
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    // A relational database (SQLite), not the in-memory one: casting a ballot is a transaction, and the database's own unique and
    // foreign-key rules are part of what keeps it safe.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Microsoft.Data.Sqlite.SqliteConnection> Connections = new();

    private static (PollEngine Engine, string Db) Create(Action<Sdk.DbContexts.AlumniDbContext> seed)
    {
        var (db, connection) = TestDb.CreateRelational(Tenant);
        var name = TestDb.NewName();
        Connections[name] = connection;
        seed(db); db.SaveChanges(); db.Dispose();
        return (Engine(name), name);
    }

    private static Sdk.DbContexts.AlumniDbContext Raw(string name) => TestDb.OpenRelational(Connections[name], Tenant);

    private static PollEngine Engine(string name)
    {
        var ctx = Raw(name);
        return new PollEngine(
            new AlumniPgRepository<Poll>(ctx), new AlumniPgRepository<PollPosition>(ctx), new AlumniPgRepository<PollAspirant>(ctx),
            new AlumniPgRepository<PollParticipation>(ctx), new AlumniPgRepository<PollVote>(ctx),
            new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<CommunityMembership>(ctx));
    }

    private static MemberEntity Person(string id, int year = 2015, string status = "Active") =>
        new() { Id = id, FirstName = id, LastName = "X", Email = $"{id}@x.org", GraduationYear = year, Status = status, InstitutionId = Tenant };

    /// <summary>Two positions: President (Ama or Kofi) and Treasurer (Esi alone, so Yes/No).</summary>
    private static void Ballot(Sdk.DbContexts.AlumniDbContext db, Action<Poll>? tweak = null)
    {
        var poll = new Poll
        {
            Id = "p1", Title = "Executive election", Status = PollStatuses.Published, InstitutionId = Tenant,
            OpensAt = Now.AddHours(-2), ClosesAt = Now.AddHours(10),
        };
        tweak?.Invoke(poll);
        db.Add(poll);
        db.Add(new PollPosition { Id = "pres", PollId = "p1", Title = "President", SortOrder = 0, InstitutionId = Tenant });
        db.Add(new PollPosition { Id = "treas", PollId = "p1", Title = "Treasurer", SortOrder = 1, InstitutionId = Tenant });
        db.Add(new PollAspirant { Id = "ama", PollId = "p1", PositionId = "pres", Name = "Ama", SortOrder = 0, InstitutionId = Tenant });
        db.Add(new PollAspirant { Id = "kofi", PollId = "p1", PositionId = "pres", Name = "Kofi", SortOrder = 1, InstitutionId = Tenant });
        db.Add(new PollAspirant { Id = "esi", PollId = "p1", PositionId = "treas", Name = "Esi", SortOrder = 0, InstitutionId = Tenant });
    }

    private static List<BallotChoice> Full(string president = "ama", string treasurer = PollChoices.Yes) =>
    [
        new("pres", president == PollChoices.Abstain ? null : president, president == PollChoices.Abstain ? PollChoices.Abstain : PollChoices.Candidate),
        new("treas", treasurer == PollChoices.Abstain ? null : "esi", treasurer),
    ];

    [Fact]
    public async Task A_complete_ballot_is_recorded_once_and_a_second_attempt_is_refused()
    {
        var (engine, _) = Create(db => { db.Add(Person("m1")); Ballot(db); });

        Assert.Equal(CastOutcome.Recorded, await engine.CastAsync("p1", "m1", Full(), Now));
        Assert.Equal(CastOutcome.AlreadyVoted, await engine.CastAsync("p1", "m1", Full("kofi", PollChoices.No), Now));

        var tally = await engine.TallyAsync((await Poll(engine)));
        Assert.Equal(1, tally.Voted);
        Assert.Equal(1, tally.Positions[0].Options.Single(o => o.AspirantId == "ama").Votes);
        Assert.Equal(0, tally.Positions[0].Options.Single(o => o.AspirantId == "kofi").Votes);   // the second attempt left no trace
    }

    private static async Task<Poll> Poll(PollEngine _) => new() { Id = "p1", InstitutionId = Tenant, OpensAt = Now.AddHours(-2), ClosesAt = Now.AddHours(10), Status = PollStatuses.Published };

    [Fact]
    public async Task Only_active_members_in_the_audience_can_vote_and_only_while_the_poll_is_open()
    {
        var (engine, _) = Create(db =>
        {
            db.Add(Person("in", 2015)); db.Add(Person("out", 2020)); db.Add(Person("pending", 2015, "Pending"));
            Ballot(db, p => p.YearGroups = [2015]);
        });

        Assert.Equal(CastOutcome.NotEligible, await engine.CastAsync("p1", "out", Full(), Now));
        Assert.Equal(CastOutcome.NotEligible, await engine.CastAsync("p1", "pending", Full(), Now));
        Assert.Equal(CastOutcome.NotOpen, await engine.CastAsync("p1", "in", Full(), Now.AddDays(1)));       // after it closed
        Assert.Equal(CastOutcome.NotOpen, await engine.CastAsync("p1", "in", Full(), Now.AddDays(-1)));      // before it opened
        Assert.Equal(CastOutcome.NotFound, await engine.CastAsync("nope", "in", Full(), Now));
        Assert.Equal(CastOutcome.Recorded, await engine.CastAsync("p1", "in", Full(), Now));
    }

    [Fact]
    public async Task A_draft_or_archived_poll_takes_no_votes()
    {
        var (draft, _) = Create(db => { db.Add(Person("m1")); Ballot(db, p => p.Status = PollStatuses.Draft); });
        Assert.Equal(CastOutcome.NotFound, await draft.CastAsync("p1", "m1", Full(), Now));
        var (archived, _) = Create(db => { db.Add(Person("m1")); Ballot(db, p => p.ArchivedAt = Now); });
        Assert.Equal(CastOutcome.NotFound, await archived.CastAsync("p1", "m1", Full(), Now));
    }

    [Fact]
    public async Task Every_position_must_be_answered_and_every_answer_must_be_a_real_option()
    {
        var (engine, _) = Create(db => { db.Add(Person("m1")); Ballot(db); });

        Assert.Equal(CastOutcome.Incomplete, await engine.CastAsync("p1", "m1", [new BallotChoice("pres", "ama", PollChoices.Candidate)], Now));
        Assert.Equal(CastOutcome.Invalid, await engine.CastAsync("p1", "m1", Full("esi"), Now));                                  // an aspirant of another position
        Assert.Equal(CastOutcome.Invalid, await engine.CastAsync("p1", "m1", [new("pres", "ama", PollChoices.Yes), new("treas", "esi", PollChoices.Yes)], Now));   // Yes on a contested position
        Assert.Equal(CastOutcome.Invalid, await engine.CastAsync("p1", "m1", [new("pres", "ama", PollChoices.Candidate), new("treas", "esi", PollChoices.Candidate)], Now)); // a candidate vote on a Yes/No
        Assert.Equal(CastOutcome.Invalid, await engine.CastAsync("p1", "m1", [new("pres", "ama", PollChoices.Candidate), new("pres", "kofi", PollChoices.Candidate), new("treas", "esi", PollChoices.Yes)], Now)); // two answers for one position
        Assert.Equal(CastOutcome.Invalid, await engine.CastAsync("p1", "m1", [new("pres", "ama", PollChoices.Candidate), new("ghost", null, PollChoices.Abstain), new("treas", "esi", PollChoices.Yes)], Now));
        // None of those used up the member's one vote.
        Assert.Equal(CastOutcome.Recorded, await engine.CastAsync("p1", "m1", Full(PollChoices.Abstain, PollChoices.Abstain), Now));
    }

    [Fact]
    public async Task A_group_poll_is_open_only_to_the_groups_approved_members()
    {
        var (engine, _) = Create(db =>
        {
            db.Add(Person("member")); db.Add(Person("asked")); db.Add(Person("outsider"));
            db.Add(new CommunityMembership { CommunityId = "g1", MemberId = "member", Status = "Approved", InstitutionId = Tenant });
            db.Add(new CommunityMembership { CommunityId = "g1", MemberId = "asked", Status = "Pending", InstitutionId = Tenant });
            Ballot(db, p => p.CommunityId = "g1");
        });

        Assert.Equal(CastOutcome.Recorded, await engine.CastAsync("p1", "member", Full(), Now));
        Assert.Equal(CastOutcome.NotEligible, await engine.CastAsync("p1", "asked", Full(), Now));
        Assert.Equal(CastOutcome.NotEligible, await engine.CastAsync("p1", "outsider", Full(), Now));
    }

    [Fact]
    public async Task The_ballot_is_secret_a_vote_row_has_no_member_and_carries_the_polls_opening_time()
    {
        var (engine, name) = Create(db => { db.Add(Person("m1")); Ballot(db); });
        await engine.CastAsync("p1", "m1", Full(), Now);

        using var db = Raw(name);
        var votes = await db.PollVotes.ToListAsync();
        Assert.Equal(2, votes.Count);
        Assert.All(votes, v => Assert.Equal(Now.AddHours(-2), v.CreatedAt));        // not when this member voted
        Assert.All(votes, v => Assert.DoesNotContain("m1", new[] { v.CreatedBy, v.AspirantId ?? "", v.PositionId, v.Choice }));
        var mark = Assert.Single(await db.PollParticipations.ToListAsync());
        Assert.Equal("m1", mark.MemberId);
    }

    [Fact]
    public async Task Figures_count_candidates_yes_no_and_abstentions_report_turnout_and_never_pick_a_winner_in_a_tie()
    {
        var (engine, _) = Create(db =>
        {
            foreach (var id in new[] { "a", "b", "c", "d" }) db.Add(Person(id));
            db.Add(Person("didnt"));
            Ballot(db);
        });
        await engine.CastAsync("p1", "a", Full("ama", PollChoices.Yes), Now);
        await engine.CastAsync("p1", "b", Full("kofi", PollChoices.Yes), Now);
        await engine.CastAsync("p1", "c", Full("ama", PollChoices.No), Now);
        await engine.CastAsync("p1", "d", Full(PollChoices.Abstain, PollChoices.Abstain), Now);

        var t = await engine.TallyAsync(await Poll(engine));
        Assert.Equal(4, t.Voted);
        Assert.Equal(5, t.Eligible);

        var president = t.Positions[0];
        Assert.False(president.IsYesNo);
        Assert.Equal(2, president.Options.Single(o => o.AspirantId == "ama").Votes);
        Assert.Equal(1, president.Abstained);
        Assert.True(president.Options[0].Leads);
        Assert.False(president.Tied);

        var treasurer = t.Positions[1];
        Assert.True(treasurer.IsYesNo);
        Assert.Equal((2, 1, 1), (treasurer.Yes, treasurer.No, treasurer.Abstained));
        Assert.True(treasurer.Passed);
    }

    [Fact]
    public async Task Equal_votes_are_a_tie_with_no_leader_and_equal_yes_and_no_do_not_pass()
    {
        var (engine, _) = Create(db => { db.Add(Person("a")); db.Add(Person("b")); Ballot(db); });
        await engine.CastAsync("p1", "a", Full("ama", PollChoices.Yes), Now);
        await engine.CastAsync("p1", "b", Full("kofi", PollChoices.No), Now);

        var t = await engine.TallyAsync(await Poll(engine));
        Assert.True(t.Positions[0].Tied);
        Assert.DoesNotContain(t.Positions[0].Options, o => o.Leads);
        Assert.True(t.Positions[1].Tied);
        Assert.False(t.Positions[1].Passed);
    }
}
