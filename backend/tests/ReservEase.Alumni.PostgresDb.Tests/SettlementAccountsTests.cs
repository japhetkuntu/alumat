using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.PostgresDb.Tests;

/// <summary>Which account a payment settles into: a group's or year group's own account only once the platform has approved it.</summary>
public class SettlementAccountsTests
{
    private const string Tenant = "inst-1";

    private static (AlumniPgRepository<Batch> batches, AlumniPgRepository<Community> groups) Seeded()
    {
        var name = TestDb.NewName();
        using (var seed = TestDb.Create(name, Tenant))
        {
            seed.Add(new Community { Id = "live", Name = "Live", PayoutStatus = "Approved", PaystackSubaccountCode = "ACCT_g", InstitutionId = Tenant });
            seed.Add(new Community { Id = "pending", Name = "Pending", PayoutStatus = "Pending", PaystackSubaccountCode = "ACCT_p", InstitutionId = Tenant });
            seed.Add(new Community { Id = "main", Name = "Main", PayoutStatus = "Approved", UseInstitutionAccount = true, InstitutionId = Tenant });
            seed.Add(new Community { Id = "other", Name = "Elsewhere", PayoutStatus = "Approved", PaystackSubaccountCode = "ACCT_o", InstitutionId = "inst-2" });
            seed.Add(new Batch { Id = "b", Name = "2019", Year = 2019, PayoutStatus = "Approved", PaystackSubaccountCode = "ACCT_b", InstitutionId = Tenant });
            seed.SaveChanges();
        }
        var db = TestDb.Create(name, Tenant);
        return (new AlumniPgRepository<Batch>(db), new AlumniPgRepository<Community>(db));
    }

    [Fact]
    public async Task A_group_settles_into_its_own_account_only_when_approved_and_not_set_to_the_institutions()
    {
        var (batches, groups) = Seeded();
        Assert.Equal("ACCT_g", await SettlementAccounts.ForAudienceAsync(batches, groups, Tenant, null, "live"));
        Assert.Null(await SettlementAccounts.ForAudienceAsync(batches, groups, Tenant, null, "pending"));
        Assert.Null(await SettlementAccounts.ForAudienceAsync(batches, groups, Tenant, null, "main"));
        Assert.Null(await SettlementAccounts.ForAudienceAsync(batches, groups, Tenant, null, "missing"));
        Assert.Null(await SettlementAccounts.ForAudienceAsync(batches, groups, Tenant, null, "other"));   // another institution's group is never used
    }

    [Fact]
    public async Task A_group_takes_precedence_over_a_year_group_and_a_group_without_an_account_does_not_borrow_it()
    {
        var (batches, groups) = Seeded();
        Assert.Equal("ACCT_g", await SettlementAccounts.ForAudienceAsync(batches, groups, Tenant, [2019], "live"));
        Assert.Null(await SettlementAccounts.ForAudienceAsync(batches, groups, Tenant, [2019], "pending"));
        Assert.Equal("ACCT_b", await SettlementAccounts.ForAudienceAsync(batches, groups, Tenant, [2019], null));
        Assert.Null(await SettlementAccounts.ForAudienceAsync(batches, groups, Tenant, [2019, 2020], null));   // more than one year group: the institution's
        Assert.Null(await SettlementAccounts.ForAudienceAsync(batches, groups, null, null, "live"));
    }

    [Fact]
    public async Task A_service_goes_where_it_says_a_groups_service_defaults_to_the_group_and_a_missing_account_means_the_institution()
    {
        var (batches, groups) = Seeded();
        Assert.Null(await SettlementAccounts.ForServiceAsync(batches, groups, Tenant, null, null));                       // plain service: institution
        Assert.Equal("ACCT_g", await SettlementAccounts.ForServiceAsync(batches, groups, Tenant, null, "live"));          // for a group: its account
        Assert.Null(await SettlementAccounts.ForServiceAsync(batches, groups, Tenant, null, "pending"));                  // group without a live account
        Assert.Null(await SettlementAccounts.ForServiceAsync(batches, groups, Tenant, "institution", "live"));            // forced to the institution
        Assert.Equal("ACCT_g", await SettlementAccounts.ForServiceAsync(batches, groups, Tenant, "group:live", null));    // another group's account
        Assert.Equal("ACCT_b", await SettlementAccounts.ForServiceAsync(batches, groups, Tenant, "year:2019", "live"));   // a year group's account
        Assert.Null(await SettlementAccounts.ForServiceAsync(batches, groups, Tenant, "year:2031", null));                // no such year group
        Assert.Null(await SettlementAccounts.ForServiceAsync(batches, groups, Tenant, "nonsense", null));
    }
}
