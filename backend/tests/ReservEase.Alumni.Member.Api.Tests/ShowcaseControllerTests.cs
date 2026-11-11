using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Controllers;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

/// <summary>What the public website shows of a featured institution: only what it agreed to, never a zero, never anyone who was not chosen.</summary>
public class ShowcaseControllerTests
{
    private static async Task<ShowcaseResponse> Read(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name)) { foreach (var e in seed) db.Add(e); db.SaveChanges(); }
        var ctx = TestDb.Create(name);
        var controller = new ShowcaseController(
            new AlumniPgRepository<Institution>(ctx),
            new ShowcaseStatsReader(new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<Community>(ctx), new AlumniPgRepository<Batch>(ctx),
                new AlumniPgRepository<AlumniEvent>(ctx), new AlumniPgRepository<Contribution>(ctx)),
            new MemoryCache(new MemoryCacheOptions()),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MemberPortalBaseDomain"] = "alumunion.com" }).Build());
        var result = (ObjectResult)await controller.Get();
        return ((ApiResponse<ShowcaseResponse>)result.Value!).Data!;
    }

    private static MemberEntity Active(string id, string institution) => new() { Id = id, InstitutionId = institution, FirstName = id, LastName = "X", Email = $"{id}@x.test", Status = "Active" };

    [Fact]
    public async Task Only_featured_active_institutions_appear_in_the_order_chosen()
    {
        var r = await Read(
            new Institution { Id = "a", Name = "Beta College", Slug = "beta", Status = "Active", FeaturedOnWebsite = true, FeaturedOrder = 2 },
            new Institution { Id = "b", Name = "Alpha University", Slug = "alpha", Status = "Active", FeaturedOnWebsite = true, FeaturedOrder = 1 },
            new Institution { Id = "c", Name = "Not chosen", Slug = "nc", Status = "Active" },
            new Institution { Id = "d", Name = "Suspended", Slug = "sus", Status = "Suspended", FeaturedOnWebsite = true });

        Assert.Equal(["Alpha University", "Beta College"], r.Institutions.Select(i => i.Name));
        Assert.Equal("https://alpha.alumunion.com", r.Institutions[0].PortalUrl);
    }

    [Fact]
    public async Task A_number_is_shown_only_when_agreed_and_never_when_zero()
    {
        var r = await Read(
            new Institution { Id = "a", Name = "UMaT", Slug = "umat", Status = "Active", FeaturedOnWebsite = true, FeaturedStats = ["members", "events"] },
            Active("m1", "a"), Active("m2", "a"), new Community { Id = "g", InstitutionId = "a", Name = "Chapter" },
            new Contribution { InstitutionId = "a", CampaignId = "c", MemberId = "m1", Status = "Successful", Amount = 500 });

        var umat = Assert.Single(r.Institutions);
        Assert.Equal(2, umat.Members);
        Assert.Null(umat.Groups);   // it has one, but did not agree to show it
        Assert.Null(umat.Events);   // agreed, but none held, and zero is not shown
        Assert.Null(umat.Raised);   // it raised money, but did not agree to show it
    }

    [Fact]
    public async Task The_totals_add_up_only_what_is_actually_shown()
    {
        var r = await Read(
            new Institution { Id = "a", Name = "A", Slug = "a", Status = "Active", FeaturedOnWebsite = true, FeaturedStats = ["members"] },
            new Institution { Id = "b", Name = "B", Slug = "b", Status = "Active", FeaturedOnWebsite = true, FeaturedStats = ["members", "groups"] },
            Active("m1", "a"), Active("m2", "b"), Active("m3", "b"),
            new Community { Id = "ga", InstitutionId = "a", Name = "Hidden" }, new Community { Id = "gb", InstitutionId = "b", Name = "Shown" });

        Assert.Equal((3, 1), (r.Totals.Members, r.Totals.Groups));   // A's group is not counted: A did not agree to show groups
    }

    [Fact]
    public async Task With_nobody_featured_the_list_is_empty()
    {
        var r = await Read(new Institution { Id = "a", Name = "A", Slug = "a", Status = "Active" });

        Assert.Empty(r.Institutions);
    }
}
