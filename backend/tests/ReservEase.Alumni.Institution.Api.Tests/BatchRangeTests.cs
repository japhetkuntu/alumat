using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>Adding many year groups at once: every missing year in a range, leaving existing ones exactly as they are.</summary>
public class BatchRangeTests
{
    private const string Tenant = "inst-1";

    private static (BatchService Service, Func<List<Batch>> All) Create(params Batch[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant)) { db.AddRange(seed); db.SaveChanges(); }
        var ctx = TestDb.Create(name, Tenant);
        return (new BatchService(new AlumniPgRepository<Batch>(ctx), NullLogger<BatchService>.Instance), () => ctx.Batches.OrderBy(b => b.Year).ToList());
    }

    private static CreateBatchRangeRequest Range(int from, int to, string pattern = "Batch of {year}") => new() { FromYear = from, ToYear = to, NamePattern = pattern };

    [Fact]
    public async Task Every_year_in_the_range_is_added_except_the_ones_that_already_exist()
    {
        var (service, all) = Create(new Batch { Year = 2016, Name = "Class of 2016 (special)", IsActive = false, PayoutStatus = "Approved" });

        var result = (await service.CreateBatchRangeAsync(Range(1989, 2026), "admin")).Data!;

        Assert.Equal(37, result.Created);
        Assert.Equal([2016], result.SkippedYears);
        var batches = all();
        Assert.Equal(38, batches.Count);
        Assert.Equal(Enumerable.Range(1989, 38), batches.Select(b => b.Year));
        var existing = batches.Single(b => b.Year == 2016);
        Assert.Equal(("Class of 2016 (special)", false, "Approved"), (existing.Name, existing.IsActive, existing.PayoutStatus));   // untouched
        Assert.Equal("Batch of 1989", batches[0].Name);
        Assert.All(batches.Where(b => b.Year != 2016), b => Assert.True(b.IsActive));
    }

    [Fact]
    public async Task Running_the_same_range_again_adds_nothing()
    {
        var (service, all) = Create();
        await service.CreateBatchRangeAsync(Range(2000, 2005), "admin");

        var again = await service.CreateBatchRangeAsync(Range(2000, 2008), "admin");

        Assert.Equal(3, again.Data!.Created);
        Assert.Equal([2000, 2001, 2002, 2003, 2004, 2005], again.Data.SkippedYears);
        Assert.Equal(9, all().Count);
        var none = (await service.CreateBatchRangeAsync(Range(2000, 2008), "admin")).Data!;
        Assert.Equal(0, none.Created);
    }

    [Theory]
    [InlineData("Batch of {year}", "Batch of 2016")]
    [InlineData("Class of", "Class of 2016")]
    [InlineData("Set {year} alumni", "Set 2016 alumni")]
    [InlineData("  Year group {YEAR}  ", "Year group 2016")]
    public async Task The_name_pattern_gets_the_year_in_the_right_place(string pattern, string expected)
    {
        var (service, all) = Create();

        await service.CreateBatchRangeAsync(Range(2016, 2016, pattern), "admin");

        Assert.Equal(expected, all().Single().Name);
    }

    [Fact]
    public async Task A_single_year_range_works_and_a_reversed_or_huge_one_is_refused()
    {
        var (service, all) = Create();

        Assert.Equal(1, (await service.CreateBatchRangeAsync(Range(2010, 2010), "admin")).Data!.Created);
        Assert.Equal(400, (await service.CreateBatchRangeAsync(Range(2020, 2010), "admin")).Code);
        Assert.Equal(400, (await service.CreateBatchRangeAsync(Range(1900, 2200), "admin")).Code);
        Assert.Equal(400, (await service.CreateBatchRangeAsync(Range(2010, 2012, "   "), "admin")).Code);
        Assert.Single(all());
    }

    [Fact]
    public void A_long_name_is_cut_to_the_length_the_database_allows()
    {
        Assert.Equal(100, BatchService.NameFor(new string('x', 150) + " {year}", 2016).Length);
    }
}
