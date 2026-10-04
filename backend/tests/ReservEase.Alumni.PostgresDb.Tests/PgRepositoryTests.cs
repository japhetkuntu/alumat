using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.PostgresDb.Tests;

public class PgRepositoryTests
{
    private static (AlumniPgRepository<Job> repo, string db) Create(string tenant = "A", string? db = null)
    {
        db ??= TestDb.NewName();
        return (new AlumniPgRepository<Job>(TestDb.Create(db, tenant)), db);
    }

    private static async Task Seed(AlumniPgRepository<Job> repo, int count = 25)
    {
        await repo.AddRangeAsync(Enumerable.Range(1, count).Select(i => new Job
        {
            Id = $"job-{i:D3}", Title = $"Job {i:D3}", Company = i % 2 == 0 ? "Acme" : "Globex", Status = i % 5 == 0 ? "Closed" : "Active",
        }).ToList());
    }

    [Fact]
    public async Task Add_then_GetById_round_trips_and_returns_the_saved_count()
    {
        var (repo, _) = Create();
        Assert.Equal(1, await repo.AddAsync(new Job { Id = "j1", Title = "T" }));
        Assert.Equal("T", (await repo.GetByIdAsync("j1"))!.Title);
    }

    [Fact]
    public async Task GetById_returns_null_for_an_unknown_id()
    {
        var (repo, _) = Create();
        Assert.Null(await repo.GetByIdAsync("nope"));
        Assert.Null(await repo.GetByIdAsync("nope", ignoreQueryFilters: true));
    }

    [Fact]
    public async Task GetById_with_ignoreQueryFilters_reaches_another_tenants_row_which_the_filter_otherwise_hides_from_queries()
    {
        var (a, db) = Create("A");
        await a.AddAsync(new Job { Id = "j1", Title = "A's" });

        var (b, _) = Create("B", db);

        Assert.Null(await b.GetOneAsync(j => j.Id == "j1"));
        Assert.Equal("A's", (await b.GetByIdAsync("j1", ignoreQueryFilters: true))!.Title);
        Assert.Equal("A's", (await b.GetOneAsync(j => j.Id == "j1", ignoreQueryFilters: true))!.Title);
    }

    [Fact]
    public async Task GetAll_without_a_predicate_returns_only_the_tenants_rows()
    {
        var (repo, db) = Create("A");
        await Seed(repo, 3);
        var (other, _) = Create("B", db);
        await other.AddAsync(new Job { Id = "other", Title = "x" });

        Assert.Equal(3, (await repo.GetAllAsync()).Count());
        Assert.Equal(4, (await repo.GetAllAsync(ignoreQueryFilters: true)).Count());
    }

    [Fact]
    public async Task GetAll_and_Count_apply_the_predicate()
    {
        var (repo, _) = Create();
        await Seed(repo);

        Assert.Equal(5, (await repo.GetAllAsync(j => j.Status == "Closed")).Count());
        Assert.Equal(5, await repo.CountAsync(j => j.Status == "Closed"));
        Assert.Equal(25, await repo.CountAsync());
    }

    [Fact]
    public async Task Predicates_may_use_TextSearch_which_the_repository_rewrites()
    {
        var (repo, _) = Create();
        await repo.AddRangeAsync([
            new Job { Id = "1", Title = "Civil Engineer", Company = "Acme Ltd" },
            new Job { Id = "2", Title = "Nurse", Company = "Acme Ltd" },
            new Job { Id = "3", Title = "Engineer", Company = "Globex" },
        ]);

        var search = "engineer acme";
        var hits = (await repo.GetAllAsync(j => TextSearch.Matches(search, j.Title, j.Company))).Select(j => j.Id).ToList();

        Assert.Equal(new[] { "1" }, hits);
        Assert.Equal(2, await repo.CountAsync(j => TextSearch.Matches("ENGINEER", j.Title, j.Company)));
        Assert.Equal(3, await repo.CountAsync(j => TextSearch.Matches("  ", j.Title)));
        Assert.Equal("1", (await repo.GetOneAsync(j => TextSearch.Matches("civil", j.Title)))!.Id);
        Assert.Equal(1, repo.GetQueryable(j => TextSearch.Matches("nurse", j.Title)).Count());
    }

    [Fact]
    public async Task Update_persists_changes_and_Remove_deletes()
    {
        var (repo, _) = Create();
        var job = new Job { Id = "j1", Title = "Before" };
        await repo.AddAsync(job);

        job.Title = "After";
        Assert.Equal(1, await repo.UpdateAsync(job));
        Assert.Equal("After", (await repo.GetByIdAsync("j1"))!.Title);

        Assert.Equal(1, await repo.RemoveAsync(job));
        Assert.Null(await repo.GetOneAsync(j => j.Id == "j1"));
    }

    [Fact]
    public async Task UpdateRange_saves_every_entity_in_one_call()
    {
        var (repo, _) = Create();
        await Seed(repo, 3);
        var all = (await repo.GetAllAsync()).ToList();
        all.ForEach(j => j.Status = "Closed");

        Assert.Equal(3, await repo.UpdateRangeAsync(all));
        Assert.Equal(3, await repo.CountAsync(j => j.Status == "Closed"));
    }

    [Fact]
    public async Task GetQueryable_is_composable_and_tenant_filtered()
    {
        var (repo, _) = Create();
        await Seed(repo, 10);

        Assert.Equal(5, repo.GetQueryable(j => j.Company == "Acme").Count());
        Assert.Equal(10, repo.GetQueryable().Count());
    }

    [Fact]
    public async Task Paging_returns_the_right_slice_and_bounds()
    {
        var (repo, _) = Create();
        await Seed(repo, 25);

        var page = await repo.GetPagedAsync(2, 10, "Title", "asc");

        Assert.Equal((2, 10, 10, 25, 3), (page.PageIndex, page.PageSize, (int)page.Count, (int)page.TotalCount, page.TotalPages));
        Assert.Equal((11, 20), (page.LowerBoundSize, page.UpperBoundSize));
        Assert.Equal("Job 011", page.Results.First().Title);
        Assert.Equal("Job 020", page.Results.Last().Title);
    }

    [Fact]
    public async Task The_last_page_is_short_and_its_upper_bound_is_the_total()
    {
        var (repo, _) = Create();
        await Seed(repo, 25);

        var page = await repo.GetPagedAsync(3, 10, "Title", "asc");

        Assert.Equal(5, page.Results.Count());
        Assert.Equal((21, 25), (page.LowerBoundSize, page.UpperBoundSize));
    }

    [Fact]
    public async Task Sorting_descending_is_the_default_and_anything_not_asc_means_descending()
    {
        var (repo, _) = Create();
        await Seed(repo, 5);

        Assert.Equal("Job 005", (await repo.GetPagedAsync(1, 5, "Title")).Results.First().Title);
        Assert.Equal("Job 005", (await repo.GetPagedAsync(1, 5, "Title", "sideways")).Results.First().Title);
        Assert.Equal("Job 001", (await repo.GetPagedAsync(1, 5, "Title", "ASC")).Results.First().Title);
        Assert.Equal("Job 005", (await repo.GetPagedAsync(1, 5, "Title", null!)).Results.First().Title);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    public async Task A_page_index_below_one_becomes_one(int requested, int expected)
    {
        var (repo, _) = Create();
        await Seed(repo, 5);
        Assert.Equal(expected, (await repo.GetPagedAsync(requested, 5)).PageIndex);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1000, 200)]
    [InlineData(200, 200)]
    [InlineData(37, 37)]
    public async Task Page_size_is_clamped_to_a_sane_range(int requested, int expected)
    {
        var (repo, _) = Create();
        await Seed(repo, 3);
        Assert.Equal(expected, (await repo.GetPagedAsync(1, requested)).PageSize);
    }

    [Theory]
    [InlineData("Title; DROP TABLE Jobs")]
    [InlineData("1=1")]
    [InlineData("Title desc, Id")]
    [InlineData("")]
    [InlineData("_Title")]
    [InlineData("Ti tle")]
    public async Task Unsafe_sort_columns_fall_back_to_Id_instead_of_reaching_dynamic_linq(string column)
    {
        var (repo, _) = Create();
        await Seed(repo, 5);

        var page = await repo.GetPagedAsync(1, 5, column, "asc");

        Assert.Equal("job-001", page.Results.First().Id);
        Assert.Equal(5, page.TotalCount);
    }

    [Fact]
    public async Task A_filter_applies_to_both_the_count_and_the_page()
    {
        var (repo, _) = Create();
        await Seed(repo, 25);

        var page = await repo.GetPagedAsync(1, 10, "Id", "asc", j => j.Company == "Acme");

        Assert.Equal(12, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.All(page.Results, j => Assert.Equal("Acme", j.Company));
    }

    [Fact]
    public async Task An_empty_result_has_zero_pages_and_a_zero_upper_bound()
    {
        var (repo, _) = Create();

        var page = await repo.GetPagedAsync(1, 10);

        Assert.Equal((0, 0, 0), (page.TotalPages, (int)page.TotalCount, page.UpperBoundSize));
        Assert.Empty(page.Results);
    }

    [Fact]
    public async Task Paging_ignores_other_tenants_unless_asked_to_ignore_the_filter()
    {
        var (repo, db) = Create("A");
        await Seed(repo, 3);
        var (other, _) = Create("B", db);
        await other.AddAsync(new Job { Id = "zzz", Title = "B's" });

        Assert.Equal(3, (await repo.GetPagedAsync(1, 10)).TotalCount);
        Assert.Equal(4, (await repo.GetPagedAsync(1, 10, ignoreQueryFilters: true)).TotalCount);
    }
}
