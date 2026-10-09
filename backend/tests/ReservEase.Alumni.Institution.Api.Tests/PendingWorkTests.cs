using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>"What are members waiting on me for?" and "does the member hear back?" for the business directory.</summary>
public class PendingWorkTests
{
    private const string Tenant = "inst-1";
    private const string Other = "inst-2";
    private static readonly AuthData Admin = new() { Id = "admin", FirstName = "Ama", LastName = "Admin", Role = "SuperAdmin" };

    private static MemberEntity Person(string id, string status, string tenant = Tenant) => new() { Id = id, InstitutionId = tenant, FirstName = "A", LastName = "B", Email = $"{id}@x.com", Status = status };
    private static Job Job_(string id, string status, string tenant = Tenant) => new() { Id = id, InstitutionId = tenant, Title = "t", Company = "c", Location = "l", Type = "Internship", Status = status, PostedBy = "m1" };
    private static BusinessListing Biz(string id, string status, bool edit = false, string? owner = "m1", string tenant = Tenant) =>
        new() { Id = id, InstitutionId = tenant, MemberId = owner, BusinessName = "Kofi Bakes", Status = status, HasPendingEdit = edit, PendingChanges = edit ? new BusinessListingPendingChanges { Description = "new" } : null };

    private static PendingWorkService Create(string db) {
        var ctx = TestDb.Create(db, Tenant);
        return new PendingWorkService(new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<Job>(ctx), new AlumniPgRepository<BusinessListing>(ctx),
            new AlumniPgRepository<Spotlight>(ctx), new AlumniPgRepository<MentorProfile>(ctx));
    }

    private static string Seed(params object[] e)
    {
        var name = TestDb.NewName();
        using var db = TestDb.Create(name, Tenant); foreach (var x in e) db.Add(x); db.SaveChanges();
        return name;
    }

    [Fact]
    public async Task Counts_each_kind_and_only_this_institutions()
    {
        var db = Seed(Person("m1", "Pending"), Person("m2", "Pending"), Person("m3", "Active"), Person("x", "Pending", Other),
            Job_("j1", "Pending"), Job_("j2", "Active"), Biz("b1", "Pending"), Biz("b2", "Approved", edit: true), Biz("b3", "Approved"), Biz("b4", "Pending", tenant: Other));

        var items = (await Create(db).GetAsync([])).ToDictionary(i => i.Key);

        Assert.Equal(2, items["members"].Count);
        Assert.Equal("2 new members waiting for approval", items["members"].Label);
        Assert.Equal("1 suggested opportunity to review", items["opportunities"].Label);
        Assert.Equal(2, items["businesses"].Count);
        Assert.DoesNotContain("spotlights", items.Keys);
    }

    [Fact]
    public async Task Leaves_out_a_feature_the_institution_has_switched_off_and_empty_kinds()
    {
        var db = Seed(Job_("j1", "Pending"), Biz("b1", "Pending"));

        var keys = (await Create(db).GetAsync(["Jobs"])).Select(i => i.Key).ToList();

        Assert.Equal(["businesses"], keys);
    }

    [Fact]
    public async Task Nothing_waiting_gives_an_empty_list()
        => Assert.Empty(await Create(Seed(Person("m1", "Active"), Job_("j1", "Active"))).GetAsync([]));

    private static (BusinessDirectoryService s, string db) Directory(params object[] seed)
    {
        var name = Seed(seed);
        var ctx = TestDb.Create(name, Tenant);
        return (new BusinessDirectoryService(new AlumniPgRepository<BusinessListing>(ctx), new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<Notification>(ctx),
            Mock.Of<IStorageService>(), TestDb.Tenant(Tenant), Mock.Of<IRedisService<PublicContentCacheConfig>>(), NullLogger<BusinessDirectoryService>.Instance), name);
    }

    [Fact]
    public async Task Approving_a_listing_tells_its_owner()
    {
        var (s, db) = Directory(Biz("b1", "Pending"));
        Assert.Equal(200, (await s.ApproveListingAsync("b1", Admin)).Code);
        using var check = TestDb.Create(db, Tenant);
        var n = Assert.Single(check.Set<Notification>().ToList());
        Assert.Equal(("m1", "Your business listing is live"), (n.RecipientId, n.Title));
    }

    [Fact]
    public async Task Rejecting_a_listing_passes_on_the_administrators_note()
    {
        var (s, db) = Directory(Biz("b1", "Pending"));
        Assert.Equal(200, (await s.RejectListingAsync("b1", "Please add a phone number", Admin)).Code);
        using var check = TestDb.Create(db, Tenant);
        Assert.Contains("Please add a phone number", check.Set<Notification>().Single().Body);
    }

    [Fact]
    public async Task Edits_are_answered_too_and_an_admin_made_listing_tells_nobody()
    {
        var (s, db) = Directory(Biz("b1", "Approved", edit: true), Biz("b2", "Pending", owner: null));
        Assert.Equal(200, (await s.ApproveEditAsync("b1", Admin)).Code);
        Assert.Equal(200, (await s.ApproveListingAsync("b2", Admin)).Code);
        using var check = TestDb.Create(db, Tenant);
        Assert.Equal("Your listing changes are live", Assert.Single(check.Set<Notification>().ToList()).Title);
    }
}
