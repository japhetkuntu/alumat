using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.PostgresDb.Tests;

/// <summary>The next member number is found from the highest ones, without reading every member who already has one.</summary>
public class MemberNumbersTests
{
    private const string Tenant = "inst-1";
    private const string Prefix = "UMAT-2018-";

    private static AlumniPgRepository<MemberEntity> Repo(params string?[] numbers)
    {
        var db = TestDb.Create(TestDb.NewName(), Tenant);
        var i = 0;
        foreach (var n in numbers)
            db.Add(new MemberEntity { Id = $"m{i++}", Email = $"m{i}@x.com", MemberNumber = n });
        db.SaveChanges();
        return new AlumniPgRepository<MemberEntity>(db);
    }

    [Fact]
    public async Task With_no_numbers_yet_the_first_is_0001()
    {
        Assert.Equal("UMAT-2018-0001", await MemberNumbers.NextAsync(Repo().GetQueryable(), Prefix));
        Assert.Equal("UMAT-2018-0001", await MemberNumbers.NextAsync(Repo("UMAT-2019-0009", null).GetQueryable(), Prefix));   // other years and members without a number do not count
    }

    [Fact]
    public async Task The_next_number_follows_the_highest_one_not_the_count()
    {
        var repo = Repo("UMAT-2018-0001", "UMAT-2018-0007", "UMAT-2018-0003");

        Assert.Equal("UMAT-2018-0008", await MemberNumbers.NextAsync(repo.GetQueryable(), Prefix));
    }

    [Fact]
    public async Task Numbers_past_9999_still_sort_as_numbers()
    {
        var repo = Repo("UMAT-2018-9999", "UMAT-2018-10000", "UMAT-2018-0002");

        Assert.Equal(10000, await MemberNumbers.MaxSequenceAsync(repo.GetQueryable(), Prefix));
        Assert.Equal("UMAT-2018-10001", await MemberNumbers.NextAsync(repo.GetQueryable(), Prefix));
    }

    [Fact]
    public async Task A_number_with_an_unreadable_ending_counts_as_zero_and_never_hides_the_real_maximum()
    {
        var repo = Repo("UMAT-2018-0004", "UMAT-2018-LEGACY-1", "UMAT-2018-x");

        Assert.Equal(4, await MemberNumbers.MaxSequenceAsync(repo.GetQueryable(), Prefix));
    }

    [Fact]
    public async Task Many_unreadable_numbers_do_not_push_a_real_one_out_of_the_window()
    {
        var odd = Enumerable.Range(0, 40).Select(i => $"UMAT-2018-ZZ-LEGACY-{i:D3}").ToList();
        var repo = Repo([.. odd, "UMAT-2018-0012"]);

        Assert.Equal(12, await MemberNumbers.MaxSequenceAsync(repo.GetQueryable(), Prefix));
    }

    [Fact]
    public async Task A_community_prefix_without_a_year_works_the_same()
    {
        var repo = Repo("CLUB-0001", "CLUB-0002", "UMAT-2018-0009");

        Assert.Equal("CLUB-0003", await MemberNumbers.NextAsync(repo.GetQueryable(), "CLUB-"));
    }
}
