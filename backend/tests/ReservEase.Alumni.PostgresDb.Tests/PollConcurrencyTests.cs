using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Polls;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.PostgresDb.Tests;

/// <summary>
/// The database's own rules behind a vote (SQLite, always run), and the real thing under load: many people voting at the same instant,
/// and an admin replacing the ballot while they do, against a real PostgreSQL. The load tests run only when ALUMNI_PG_TEST_ADMIN names a
/// server (e.g. "Host=localhost;Port=5433;Username=admin;Password=admin"); they create a throwaway database and drop it afterwards.
/// </summary>
public class PollConcurrencyTests
{
    private const string Tenant = "inst-1";
    private static readonly DateTime Now = DateTime.UtcNow;

    private static void SeedBallot(AlumniDbContext db, int members)
    {
        for (var i = 0; i < members; i++) db.Add(new MemberEntity { Id = $"m{i}", FirstName = $"M{i}", LastName = "X", Email = $"m{i}@x.org", GraduationYear = 2015, Status = "Active", InstitutionId = Tenant });
        db.Add(new Poll { Id = "p1", Title = "Election", Status = PollStatuses.Published, InstitutionId = Tenant, OpensAt = Now.AddHours(-1), ClosesAt = Now.AddHours(5) });
        db.Add(new PollPosition { Id = "pres", PollId = "p1", Title = "President", InstitutionId = Tenant });
        db.Add(new PollPosition { Id = "treas", PollId = "p1", Title = "Treasurer", SortOrder = 1, InstitutionId = Tenant });
        db.Add(new PollAspirant { Id = "ama", PollId = "p1", PositionId = "pres", Name = "Ama", InstitutionId = Tenant });
        db.Add(new PollAspirant { Id = "kofi", PollId = "p1", PositionId = "pres", Name = "Kofi", SortOrder = 1, InstitutionId = Tenant });
        db.Add(new PollAspirant { Id = "esi", PollId = "p1", PositionId = "treas", Name = "Esi", InstitutionId = Tenant });
    }

    private static PollEngine Engine(AlumniDbContext c) => new(
        new AlumniPgRepository<Poll>(c), new AlumniPgRepository<PollPosition>(c), new AlumniPgRepository<PollAspirant>(c), new AlumniPgRepository<PollParticipation>(c),
        new AlumniPgRepository<PollVote>(c), new AlumniPgRepository<MemberEntity>(c), new AlumniPgRepository<CommunityMembership>(c));

    private static List<BallotChoice> Ballot(string president = "ama") => [new("pres", president, PollChoices.Candidate), new("treas", "esi", PollChoices.Yes)];

    // ── Always run: the rules the database enforces ─────────────────────────

    [Fact]
    public void The_database_refuses_a_second_participation_for_the_same_member()
    {
        var (db, connection) = TestDb.CreateRelational(Tenant);
        using var _ = connection;
        SeedBallot(db, 1);
        db.Add(new PollParticipation { PollId = "p1", MemberId = "m0", InstitutionId = Tenant });
        db.SaveChanges();

        db.Add(new PollParticipation { PollId = "p1", MemberId = "m0", InstitutionId = Tenant });
        var e = Assert.Throws<DbUpdateException>(() => db.SaveChanges());
        Assert.True(DbErrors.IsUniqueViolation(e));
        Assert.False(DbErrors.IsForeignKeyViolation(e));
    }

    [Fact]
    public void A_choice_with_votes_on_it_cannot_be_deleted_so_a_ballot_cannot_be_replaced_under_a_vote()
    {
        var (db, connection) = TestDb.CreateRelational(Tenant);
        using var _ = connection;
        SeedBallot(db, 1);
        db.Add(new PollVote { PollId = "p1", PositionId = "pres", AspirantId = "ama", Choice = PollChoices.Candidate, InstitutionId = Tenant });
        db.SaveChanges();

        var aspirants = new AlumniPgRepository<PollAspirant>(db);
        var e = Record.Exception(() => aspirants.ExecuteDeleteAsync(a => a.PollId == "p1").GetAwaiter().GetResult());
        Assert.NotNull(e);
        Assert.True(DbErrors.IsForeignKeyViolation(e!));
        Assert.Equal(3, db.PollAspirants.Count());                      // nothing was removed
    }

    [Fact]
    public void A_vote_for_a_choice_that_no_longer_exists_is_refused_by_the_database()
    {
        var (db, connection) = TestDb.CreateRelational(Tenant);
        using var _ = connection;
        SeedBallot(db, 1);
        db.SaveChanges();
        new AlumniPgRepository<PollAspirant>(db).ExecuteDeleteAsync(a => a.Id == "kofi").GetAwaiter().GetResult();

        db.Add(new PollVote { PollId = "p1", PositionId = "pres", AspirantId = "kofi", Choice = PollChoices.Candidate, InstitutionId = Tenant });
        var e = Assert.Throws<DbUpdateException>(() => db.SaveChanges());
        Assert.True(DbErrors.IsForeignKeyViolation(e));
    }

    [Fact]
    public async Task A_vote_withdrawn_because_the_poll_closed_leaves_no_trace_and_the_member_can_still_not_vote_after_closing()
    {
        var (db, connection) = TestDb.CreateRelational(Tenant);
        using var _ = connection;
        SeedBallot(db, 1);
        db.SaveChanges();
        var engine = Engine(db);

        // Submitted before closing (now), but the poll's closing time has since been brought forward to before that moment.
        new AlumniPgRepository<Poll>(db).ExecuteUpdateAsync(p => p.Id == "p1", s => s.SetProperty(p => p.ClosesAt, Now.AddHours(-2).AddMinutes(30))).GetAwaiter().GetResult();
        Assert.Equal(CastOutcome.NotOpen, await engine.CastAsync("p1", "m0", Ballot(), Now.AddHours(-3)));    // opened -1h: not yet open at -3h
        Assert.Equal(0, db.PollParticipations.Count());
        Assert.Equal(0, db.PollVotes.Count());
    }

    // ── Only with a real PostgreSQL ─────────────────────────────────────────

    private sealed class PgScratch : IAsyncDisposable
    {
        private readonly string _admin; private readonly string _name;
        public string ConnectionString { get; }
        private PgScratch(string admin, string name) { _admin = admin; _name = name; ConnectionString = $"{admin};Database={name}"; }
        public static async Task<PgScratch> CreateAsync(string admin)
        {
            var name = "race_" + Guid.NewGuid().ToString("N")[..12];
            await using (var c = new Npgsql.NpgsqlConnection(admin + ";Database=postgres")) { await c.OpenAsync(); await using var cmd = new Npgsql.NpgsqlCommand($"create database {name}", c); await cmd.ExecuteNonQueryAsync(); }
            var scratch = new PgScratch(admin, name);
            await using var db = scratch.Open(); await db.Database.EnsureCreatedAsync();
            return scratch;
        }
        public AlumniDbContext Open() => new(new DbContextOptionsBuilder<AlumniDbContext>().UseNpgsql(ConnectionString).Options, TestDb.Tenant(Tenant));
        public async ValueTask DisposeAsync()
        {
            Npgsql.NpgsqlConnection.ClearAllPools();
            await using var c = new Npgsql.NpgsqlConnection(_admin + ";Database=postgres"); await c.OpenAsync();
            await using var cmd = new Npgsql.NpgsqlCommand($"drop database if exists {_name} with (force)", c); await cmd.ExecuteNonQueryAsync();
        }
    }

    private static string? Admin => Environment.GetEnvironmentVariable("ALUMNI_PG_TEST_ADMIN");

    [Fact]
    public async Task One_member_pressing_submit_many_times_at_once_gets_exactly_one_vote()
    {
        if (Admin is null) return;
        await using var pg = await PgScratch.CreateAsync(Admin);
        await using (var seed = pg.Open()) { SeedBallot(seed, 1); await seed.SaveChangesAsync(); }

        var results = await Task.WhenAll(Enumerable.Range(0, 24).Select(async _ =>
        {
            await using var db = pg.Open();
            return await Engine(db).CastAsync("p1", "m0", Ballot(), DateTime.UtcNow);
        }));

        Assert.Equal(1, results.Count(r => r == CastOutcome.Recorded));
        Assert.Equal(23, results.Count(r => r == CastOutcome.AlreadyVoted));
        await using var check = pg.Open();
        Assert.Equal(1, await check.PollParticipations.CountAsync());
        Assert.Equal(2, await check.PollVotes.CountAsync());                       // one answer per position, not 24
    }

    [Fact]
    public async Task Many_members_voting_at_once_are_all_counted_exactly_once()
    {
        if (Admin is null) return;
        const int voters = 60;
        await using var pg = await PgScratch.CreateAsync(Admin);
        await using (var seed = pg.Open()) { SeedBallot(seed, voters); await seed.SaveChangesAsync(); }

        var results = await Task.WhenAll(Enumerable.Range(0, voters).Select(async i =>
        {
            await using var db = pg.Open();
            return await Engine(db).CastAsync("p1", $"m{i}", Ballot(i % 3 == 0 ? "kofi" : "ama"), DateTime.UtcNow);
        }));

        Assert.All(results, r => Assert.Equal(CastOutcome.Recorded, r));
        await using var check = pg.Open();
        var tally = await Engine(check).TallyAsync((await check.Polls.AsNoTracking().SingleAsync()));
        Assert.Equal(voters, tally.Voted);
        Assert.Equal(voters, tally.Positions[0].Options.Sum(o => o.Votes));
        Assert.Equal(20, tally.Positions[0].Options.Single(o => o.AspirantId == "kofi").Votes);
        Assert.Equal(voters, tally.Positions[1].Yes);
    }

    [Fact]
    public async Task An_admin_replacing_the_ballot_while_people_vote_never_leaves_a_vote_for_a_choice_that_is_gone()
    {
        if (Admin is null) return;
        const int voters = 40;
        await using var pg = await PgScratch.CreateAsync(Admin);
        await using (var seed = pg.Open()) { SeedBallot(seed, voters); await seed.SaveChangesAsync(); }

        var votes = Enumerable.Range(0, voters).Select(async i =>
        {
            await Task.Delay(i % 5);                                         // spread the votes across the moment of the swap
            await using var db = pg.Open();
            return await Engine(db).CastAsync("p1", $"m{i}", Ballot(), DateTime.UtcNow);
        }).ToList();

        // The admin's replace: the same two statements PollService runs, racing the votes above.
        var swap = Task.Run(async () =>
        {
            await Task.Delay(8);
            await using var db = pg.Open();
            try
            {
                await new AlumniPgRepository<PollAspirant>(db).ExecuteDeleteAsync(a => a.PollId == "p1");
                await new AlumniPgRepository<PollPosition>(db).ExecuteDeleteAsync(p => p.PollId == "p1");
                return true;
            }
            catch (Exception e) when (DbErrors.IsForeignKeyViolation(e) || DbErrors.IsDeadlock(e)) { return false; }       // votes were already there: the database said no
        });

        var outcomes = await Task.WhenAll(votes);
        var swapped = await swap;

        await using var check = pg.Open();
        // Nothing is orphaned and nobody is half-recorded: every vote points at a choice that still exists, and everyone marked as having
        // voted has a full ballot behind them while everyone else has nothing.
        Assert.Equal(0, await check.PollVotes.Where(v => v.AspirantId != null && !check.PollAspirants.Any(a => a.Id == v.AspirantId)).CountAsync());
        var recorded = outcomes.Count(o => o == CastOutcome.Recorded);
        Assert.Equal(recorded, await check.PollParticipations.CountAsync());
        Assert.Equal(recorded * 2, await check.PollVotes.CountAsync());
        Assert.All(outcomes, o => Assert.Contains(o, new[] { CastOutcome.Recorded, CastOutcome.BallotChanged, CastOutcome.Invalid, CastOutcome.Incomplete }));
        // And the swap and the votes agree: if the ballot was replaced, nobody who voted before it is left with votes (the swap only succeeds when there were none).
        if (swapped) Assert.Equal(0, recorded);
    }
}
