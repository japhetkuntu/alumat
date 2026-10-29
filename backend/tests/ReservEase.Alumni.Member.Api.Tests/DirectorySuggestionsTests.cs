using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

public class DirectorySuggestionsTests
{
    private const string Tenant = "inst-1";

    private static DirectoryService Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant)) { foreach (var e in seed) db.Add(e); db.SaveChanges(); }
        var ctx = TestDb.Create(name, Tenant);
        return new DirectoryService(new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<Department>(ctx), NullLogger<DirectoryService>.Instance,
            new AlumniPgRepository<CommunityMembership>(ctx), new AlumniPgRepository<Community>(ctx));
    }

    private static MemberEntity Person(string id, int year = 2015, string? program = null, string status = "Active") =>
        new() { Id = id, FirstName = id, LastName = "X", Email = $"{id}@x.org", GraduationYear = year, Program = program, Status = status, InstitutionId = Tenant };

    private static CommunityMembership In(string group, string member, string status = "Approved") =>
        new() { CommunityId = group, MemberId = member, Status = status, InstitutionId = Tenant };

    [Fact]
    public async Task People_who_share_more_with_you_come_first_and_each_comes_with_the_reasons()
    {
        var service = Create(
            Person("me", 2015, "BSc Mining"), Person("both", 2015, "BSc Mining"), Person("group", 2020), Person("year", 2015), Person("stranger", 1999),
            new Community { Id = "g1", Name = "Accra Chapter", InstitutionId = Tenant }, In("g1", "me"), In("g1", "group"), In("g1", "both"));

        var list = (await service.GetSuggestionsAsync("me")).Data!;

        Assert.Equal(["both", "group", "year"], list.Select(s => s.Member.Id));
        Assert.Contains("In Accra Chapter", list[0].Reasons);
        Assert.Contains("Studied BSc Mining", list[0].Reasons);
        Assert.Contains("Class of 2015", list[0].Reasons);
        Assert.DoesNotContain(list, s => s.Member.Id is "me" or "stranger");
    }

    [Fact]
    public async Task Pending_members_and_people_who_only_asked_to_join_a_group_are_not_suggested()
    {
        var service = Create(
            Person("me", 2015), Person("waiting", 2000, status: "Pending"), Person("asked", 2000),
            new Community { Id = "g1", Name = "Choir", InstitutionId = Tenant }, In("g1", "me"), In("g1", "asked", "Pending"));

        Assert.Empty((await service.GetSuggestionsAsync("me")).Data!);
    }
}
