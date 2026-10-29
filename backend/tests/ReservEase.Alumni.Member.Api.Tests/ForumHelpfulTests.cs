using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

/// <summary>Marking a reply helpful: once per member, never your own, and it is counted for everyone to see.</summary>
public class ForumHelpfulTests
{
    private const string Tenant = "inst-1";

    private static MemberForumService Create(string name)
    {
        var db = TestDb.Create(name, Tenant);
        return new MemberForumService(
            new AlumniPgRepository<ForumCategory>(db), new AlumniPgRepository<ForumThread>(db), new AlumniPgRepository<ForumPost>(db),
            new AlumniPgRepository<CommunityMembership>(db), new AlumniPgRepository<Community>(db), new AlumniPgRepository<MemberEntity>(db),
            Mock.Of<ITemporalClientProvider>(t => t.IsAvailable == false), TestDb.Tenant(Tenant, "umat"), NullLogger<MemberForumService>.Instance,
            new AlumniPgRepository<ForumPostHelpful>(db));
    }

    private static string Seed()
    {
        var name = TestDb.NewName();
        using var db = TestDb.Create(name, Tenant);
        db.Add(new MemberEntity { Id = "asker", FirstName = "Kofi", LastName = "B", Email = "k@x.org", InstitutionId = Tenant });
        db.Add(new MemberEntity { Id = "answerer", FirstName = "Ama", LastName = "M", Email = "a@x.org", InstitutionId = Tenant });
        db.Add(new MemberEntity { Id = "reader", FirstName = "Esi", LastName = "O", Email = "e@x.org", InstitutionId = Tenant });
        db.Add(new ForumThread { Id = "t1", CategoryId = "c", Title = "Advice?", AuthorId = "asker", InstitutionId = Tenant });
        db.Add(new ForumPost { Id = "p1", ThreadId = "t1", AuthorId = "answerer", Content = "Try this.", InstitutionId = Tenant });
        db.SaveChanges();
        return name;
    }

    private static AuthData Who(string id) => new() { Id = id, Role = "Member" };

    [Fact]
    public async Task A_member_can_mark_a_reply_helpful_and_take_it_back_and_it_is_counted()
    {
        var name = Seed();
        var service = Create(name);

        var marked = await service.ToggleHelpfulAsync("p1", Who("reader"));
        Assert.Equal(200, marked.Code);
        await service.ToggleHelpfulAsync("p1", Who("asker"));

        var posts = (await service.GetPostsAsync("t1", new BaseFilter { Page = 1, PageSize = 20 }, Who("reader"))).Data!.Results;
        Assert.Equal(2, posts.Single().HelpfulCount);
        Assert.True(posts.Single().MarkedHelpfulByMe);

        await service.ToggleHelpfulAsync("p1", Who("reader"));   // taken back
        var after = (await service.GetPostsAsync("t1", new BaseFilter { Page = 1, PageSize = 20 }, Who("asker"))).Data!.Results.Single();
        Assert.Equal(1, after.HelpfulCount);
        Assert.True(after.MarkedHelpfulByMe);                     // the asker's own mark is still there
    }

    [Fact]
    public async Task You_cannot_mark_your_own_reply_and_nobody_is_notified_of_a_mark()
    {
        var name = Seed();
        var service = Create(name);

        Assert.Equal(400, (await service.ToggleHelpfulAsync("p1", Who("answerer"))).Code);
        Assert.Equal(404, (await service.ToggleHelpfulAsync("nope", Who("reader"))).Code);

        await service.ToggleHelpfulAsync("p1", Who("reader"));
        using var db = TestDb.Create(name, Tenant);
        Assert.Empty(await db.Notifications.ToListAsync());
    }
}
