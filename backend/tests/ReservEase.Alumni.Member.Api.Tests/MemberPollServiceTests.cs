using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Polls;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

/// <summary>What a member sees and can do with polls: only their own audience's, one ballot, and figures only when allowed.</summary>
public class MemberPollServiceTests
{
    private const string Tenant = "inst-1";
    private static AuthData Who(string id) => new() { Id = id, Role = "Member" };

    // A relational database (SQLite): voting is a transaction, and the database's own rules are part of what keeps it safe.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Microsoft.Data.Sqlite.SqliteConnection> Connections = new();

    private static MemberPollService Create(string name)
    {
        var db = TestDb.OpenRelational(Connections[name], Tenant);
        var engine = new PollEngine(new AlumniPgRepository<Poll>(db), new AlumniPgRepository<PollPosition>(db), new AlumniPgRepository<PollAspirant>(db),
            new AlumniPgRepository<PollParticipation>(db), new AlumniPgRepository<PollVote>(db), new AlumniPgRepository<MemberEntity>(db), new AlumniPgRepository<CommunityMembership>(db));
        return new MemberPollService(new AlumniPgRepository<Poll>(db), new AlumniPgRepository<PollPosition>(db), new AlumniPgRepository<PollAspirant>(db),
            new AlumniPgRepository<PollParticipation>(db), new AlumniPgRepository<MemberEntity>(db), new AlumniPgRepository<CommunityMembership>(db),
            new AlumniPgRepository<Community>(db), engine, NullLogger<MemberPollService>.Instance);
    }

    private static string Seed(Action<AlumniDbContext>? more = null)
    {
        var (db, connection) = TestDb.CreateRelational(Tenant);
        var name = TestDb.NewName();
        Connections[name] = connection;
        db.Add(new MemberEntity { Id = "in", FirstName = "In", LastName = "X", Email = "in@x.org", GraduationYear = 2015, Status = "Active", InstitutionId = Tenant });
        db.Add(new MemberEntity { Id = "out", FirstName = "Out", LastName = "X", Email = "out@x.org", GraduationYear = 2020, Status = "Active", InstitutionId = Tenant });
        void Poll(string id, string title, string status, int opensHrs, int closesHrs, List<int>? years, string mode = "AfterClose")
        {
            db.Add(new Poll { Id = id, Title = title, Status = status, InstitutionId = Tenant, OpensAt = DateTime.UtcNow.AddHours(opensHrs), ClosesAt = DateTime.UtcNow.AddHours(closesHrs), YearGroups = years, ResultsMode = mode });
            db.Add(new PollPosition { Id = id + "-pos", PollId = id, Title = "President", InstitutionId = Tenant });
            db.Add(new PollAspirant { Id = id + "-a", PollId = id, PositionId = id + "-pos", Name = "Ama", InstitutionId = Tenant });
            db.Add(new PollAspirant { Id = id + "-b", PollId = id, PositionId = id + "-pos", Name = "Kofi", SortOrder = 1, InstitutionId = Tenant });
        }
        Poll("open", "Open one", PollStatuses.Published, -2, 20, null);
        Poll("class15", "Class of 2015 only", PollStatuses.Published, -2, 30, [2015]);
        Poll("soon", "Opens tomorrow", PollStatuses.Published, 24, 48, null);
        Poll("done", "Finished", PollStatuses.Published, -48, -1, null);
        Poll("draft", "Not published", PollStatuses.Draft, -2, 20, null);
        Poll("live", "Live figures", PollStatuses.Published, -2, 20, null, "Live");
        more?.Invoke(db);
        db.SaveChanges();
        db.Dispose();
        return name;
    }

    [Fact]
    public async Task A_member_sees_the_published_polls_for_their_audience_open_first_and_never_a_draft()
    {
        var name = Seed();
        var inside = (await Create(name).GetPollsAsync(Who("in"))).Data!;
        var outside = (await Create(name).GetPollsAsync(Who("out"))).Data!;

        Assert.DoesNotContain(inside, p => p.Id == "draft");
        Assert.Equal("Open", inside[0].State);
        Assert.Equal("Closed", inside[^1].State);
        Assert.Contains(inside, p => p.Id == "class15");
        Assert.DoesNotContain(outside, p => p.Id == "class15");           // a Class of 2015 poll is not for 2020
        Assert.Equal(404, (await Create(name).GetPollAsync("class15", Who("out"))).Code);
        Assert.Equal(404, (await Create(name).GetPollAsync("draft", Who("in"))).Code);
    }

    [Fact]
    public async Task Voting_marks_the_member_as_having_voted_and_a_second_ballot_is_refused()
    {
        var name = Seed();
        var ballot = new CastBallotRequest { Choices = [new("open-pos", "open-a", "Candidate")] };

        Assert.Equal(200, (await Create(name).CastBallotAsync("open", ballot, Who("in"))).Code);
        Assert.Equal(409, (await Create(name).CastBallotAsync("open", ballot, Who("in"))).Code);
        Assert.Equal(403, (await Create(name).CastBallotAsync("class15", new CastBallotRequest { Choices = [new("class15-pos", "class15-a", "Candidate")] }, Who("out"))).Code);
        Assert.Equal(400, (await Create(name).CastBallotAsync("soon", new CastBallotRequest { Choices = [new("soon-pos", "soon-a", "Candidate")] }, Who("in"))).Code);   // not open yet
        Assert.Equal(400, (await Create(name).CastBallotAsync("done", new CastBallotRequest { Choices = [new("done-pos", "done-a", "Candidate")] }, Who("in"))).Code);   // closed

        var detail = (await Create(name).GetPollAsync("open", Who("in"))).Data!;
        Assert.True(detail.HasVoted);
        Assert.False(detail.CanVote);
        Assert.NotNull(detail.VotedAt);
    }

    [Fact]
    public async Task Results_stay_hidden_until_the_polls_mode_allows_them()
    {
        var name = Seed();
        Assert.Equal(403, (await Create(name).GetResultsAsync("open", Who("in"))).Code);      // AfterClose, still open
        Assert.Equal(200, (await Create(name).GetResultsAsync("live", Who("in"))).Code);      // Live
        Assert.Equal(200, (await Create(name).GetResultsAsync("done", Who("in"))).Code);      // AfterClose, closed
        Assert.Equal(403, (await Create(name).GetResultsAsync("soon", Who("in"))).Code);
    }
}
