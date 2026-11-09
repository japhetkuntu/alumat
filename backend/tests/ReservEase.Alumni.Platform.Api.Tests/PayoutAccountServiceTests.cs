using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Platform.Api.Tests;

/// <summary>The platform's record of the payout accounts communities gave us: still there after approval, filterable, with numbers masked until someone asks.</summary>
public class PayoutAccountServiceTests
{
    private static readonly DateTime Decided = new(2026, 10, 2, 9, 30, 0, DateTimeKind.Utc);

    private static object[] Seed() =>
    [
        new Institution { Id = "i1", Name = "UMaT", Slug = "umat", PayoutStatus = "Approved", PaystackSubaccountCode = "ACCT_umat", SettlementBankName = "GCB Bank",
            SettlementAccountName = "UMaT Alumni", SettlementAccountNumber = "1234567890", PayoutDecidedAt = Decided, PayoutDecidedBy = "Efua Mensah" },
        new Institution { Id = "i2", Name = "KNUST", Slug = "knust", PayoutStatus = "None" },
        new Batch { Id = "b2020", InstitutionId = "i1", Name = "Class of 2020", Year = 2020, PayoutStatus = "Approved", PaystackSubaccountCode = "ACCT_2020",
            SettlementBankName = "Fidelity Bank", SettlementAccountName = "Class of 2020 Fund", SettlementAccountNumber = "9876543210", PayoutDecidedAt = Decided, PayoutDecidedBy = "Efua Mensah" },
        new Batch { Id = "b2021", InstitutionId = "i1", Name = "Class of 2021", Year = 2021, PayoutStatus = "Approved", UseInstitutionAccount = true },
        new Community { Id = "g1", InstitutionId = "i1", Name = "Accra Chapter", PayoutStatus = "Approved", PaystackSubaccountCode = "ACCT_accra",
            SettlementBankName = "Ecobank", SettlementAccountName = "Accra Chapter", SettlementAccountNumber = "0011223344", PayoutDecidedAt = Decided, PayoutDecidedBy = "Kojo Boateng",
            // A newer submission is waiting; the approved details stay on record.
            PendingPayoutChanges = new BatchPayoutPendingChanges { SettlementBankName = "Stanbic", SettlementAccountName = "Accra Chapter Ltd", SettlementAccountNumber = "5566778899" } },
        new Community { Id = "g2", InstitutionId = "i1", Name = "Kumasi Chapter", PayoutStatus = "Pending",
            PendingPayoutChanges = new BatchPayoutPendingChanges { SettlementBankName = "CalBank", SettlementAccountName = "Kumasi Chapter", SettlementAccountNumber = "4455667788" } },
        new Community { Id = "g3", InstitutionId = "i1", Name = "Tema Chapter", PayoutStatus = "Rejected", PayoutDecisionNote = "Account name does not match", PayoutDecidedAt = Decided, PayoutDecidedBy = "Kojo Boateng" },
        new Community { Id = "g4", InstitutionId = "i1", Name = "Never set up", PayoutStatus = "None" },
        new Community { Id = "k1", InstitutionId = "i2", Name = "KNUST Kumasi", PayoutStatus = "Approved", PaystackSubaccountCode = "ACCT_knust",
            SettlementBankName = "GCB Bank", SettlementAccountName = "KNUST Kumasi", SettlementAccountNumber = "1112223334", PayoutDecidedAt = Decided, PayoutDecidedBy = "Efua Mensah" },
    ];

    private static (PayoutAccountService Service, Mock<IAuditLogService> Audit) Create()
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name)) { db.AddRange(Seed()); db.SaveChanges(); }
        var ctx = TestDb.Create(name);
        var audit = new Mock<IAuditLogService>();
        return (new PayoutAccountService(new AlumniPgRepository<Institution>(ctx), new AlumniPgRepository<Batch>(ctx), new AlumniPgRepository<Community>(ctx),
            audit.Object, NullLogger<PayoutAccountService>.Instance), audit);
    }

    [Fact]
    public async Task Approved_accounts_stay_on_record_with_who_decided_and_when_and_the_never_set_up_are_left_out()
    {
        var (s, _) = Create();

        var all = (await s.GetAsync(null, null, null, null, 1, 50)).Data!;

        Assert.Equal(7, all.TotalCount);   // UMaT, two year groups, three groups and KNUST's group; the ones never set up are left out
        Assert.DoesNotContain(all.Results, a => a.Name == "Never set up" || a.Name == "KNUST");
        var accra = Assert.Single(all.Results, a => a.Name == "Accra Chapter");
        Assert.Equal(("Live", "Ecobank", "Accra Chapter", "ACCT_accra"), (accra.State, accra.BankName, accra.AccountName, accra.SubaccountCode));
        Assert.Equal((Decided, "Kojo Boateng"), (accra.DecidedAt, accra.DecidedBy));
    }

    [Fact]
    public async Task Account_numbers_are_masked_to_the_last_four_digits_in_the_list()
    {
        var (s, _) = Create();

        var items = (await s.GetAsync(null, null, null, null, 1, 50)).Data!.Results;

        Assert.All(items.Where(i => i.AccountNumberMasked is not null), i => Assert.StartsWith("•••• ", i.AccountNumberMasked));
        Assert.Equal("•••• 3210", items.Single(i => i.Name == "Class of 2020").AccountNumberMasked);
        Assert.DoesNotContain(items, i => i.AccountNumberMasked!?.Contains("9876543210") == true);
    }

    [Fact]
    public async Task The_list_can_be_narrowed_to_one_institution_a_kind_or_a_state()
    {
        var (s, _) = Create();

        var knust = (await s.GetAsync("i2", null, null, null, 1, 50)).Data!;
        Assert.Equal(["KNUST Kumasi"], knust.Results.Select(a => a.Name).ToArray());

        var groups = (await s.GetAsync("i1", "Group", null, null, 1, 50)).Data!;
        Assert.All(groups.Results, a => Assert.Equal("Group", a.Kind));

        var live = (await s.GetAsync("i1", null, "Live", null, 1, 50)).Data!;
        Assert.Equal(["Accra Chapter", "Class of 2020", "UMaT"], live.Results.Select(a => a.Name).Order().ToArray());

        var shares = (await s.GetAsync("i1", null, "SharesInstitution", null, 1, 50)).Data!;
        Assert.Equal(["Class of 2021"], shares.Results.Select(a => a.Name).ToArray());
    }

    [Fact]
    public async Task The_counts_cover_the_institution_chosen_whatever_the_state_filter_so_they_can_be_the_filter()
    {
        var (s, _) = Create();

        var counts = (await s.GetAsync("i1", null, "Live", null, 1, 50)).Data!.Counts;

        Assert.Equal((3, 1, 1, 1), (counts.Live, counts.Pending, counts.SharesInstitution, counts.Rejected));
    }

    [Theory]
    [InlineData("ecobank", "Accra Chapter")]       // the bank
    [InlineData("Fund", "Class of 2020")]          // the account holder's name
    [InlineData("ACCT_knust", "KNUST Kumasi")]     // the subaccount code
    [InlineData("tema", "Tema Chapter")]           // the name
    public async Task Search_finds_an_account_by_its_name_bank_account_holder_or_subaccount(string term, string expected)
    {
        var (s, _) = Create();

        var found = (await s.GetAsync(null, null, null, term, 1, 50)).Data!.Results;

        Assert.Equal(expected, Assert.Single(found).Name);
    }

    [Fact]
    public async Task A_resubmission_is_flagged_while_the_approved_details_stay_and_a_first_submission_shows_what_was_submitted()
    {
        var (s, _) = Create();
        var items = (await s.GetAsync("i1", null, null, null, 1, 50)).Data!.Results;

        var accra = items.Single(i => i.Name == "Accra Chapter");
        Assert.Equal(("Ecobank", true, false), (accra.BankName, accra.ChangeAwaitingApproval, accra.ShowingSubmitted));

        var kumasi = items.Single(i => i.Name == "Kumasi Chapter");
        Assert.Equal(("Pending", "CalBank", false, true), (kumasi.State, kumasi.BankName, kumasi.ChangeAwaitingApproval, kumasi.ShowingSubmitted));
    }

    [Fact]
    public async Task A_rejection_keeps_its_reason()
    {
        var (s, _) = Create();

        var tema = (await s.GetAsync("i1", null, "Rejected", null, 1, 50)).Data!.Results.Single();

        Assert.Equal(("Rejected", "Account name does not match"), (tema.State, tema.DecisionNote));
    }

    [Fact]
    public async Task Paging_returns_a_slice_and_the_total()
    {
        var (s, _) = Create();

        var page = (await s.GetAsync(null, null, null, null, 2, 3)).Data!;

        Assert.Equal((7, 3, 2, 3), (page.TotalCount, page.Results.Count, page.Page, page.TotalPages));
    }

    [Fact]
    public async Task Revealing_a_number_returns_it_and_leaves_a_trail_of_who_looked_at_whose_account()
    {
        var (s, audit) = Create();

        var result = await s.RevealAsync("YearGroup", "b2020", "staff1", "Efua Mensah");

        Assert.Equal("9876543210", result.Data!.AccountNumber);
        audit.Verify(a => a.LogAsync("staff1", "Efua Mensah", "viewed the account number of year group \"Class of 2020\"", "UMaT"), Times.Once);
    }

    [Theory]
    [InlineData("Group", "nope")]
    [InlineData("YearGroup", "nope")]
    [InlineData("Institution", "nope")]
    public async Task Revealing_an_unknown_account_is_404_and_logs_nothing(string kind, string id)
    {
        var (s, audit) = Create();

        Assert.Equal(404, (await s.RevealAsync(kind, id, "staff1", "Efua")).Code);
        audit.Verify(a => a.LogAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Revealing_an_account_with_no_number_or_of_an_unknown_kind_is_refused()
    {
        var (s, audit) = Create();

        Assert.Equal(404, (await s.RevealAsync("Group", "g3", "staff1", "Efua")).Code);     // rejected, nothing on record
        Assert.Equal(400, (await s.RevealAsync("Alien", "g1", "staff1", "Efua")).Code);
        audit.Verify(a => a.LogAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}

/// <summary>Approving or rejecting leaves a record of who decided and when, so it can be shown long after the pending list has emptied.</summary>
public class PayoutDecisionRecordTests
{
    private static (GroupPayoutService Service, string Db) Create()
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name))
        {
            db.Add(new Institution { Id = "i1", Name = "UMaT", Slug = "umat" });
            db.Add(new Community { Id = "g1", InstitutionId = "i1", Name = "Accra Chapter", PayoutStatus = "Pending", PendingPayoutChanges = new BatchPayoutPendingChanges { UseInstitutionAccount = true } });
            db.Add(new Community { Id = "g2", InstitutionId = "i1", Name = "Kumasi Chapter", PayoutStatus = "Pending", PendingPayoutChanges = new BatchPayoutPendingChanges { SettlementBankName = "CalBank", SettlementAccountNumber = "4455667788" } });
            db.SaveChanges();
        }
        var ctx = TestDb.Create(name);
        return (new GroupPayoutService(new AlumniPgRepository<Community>(ctx), new AlumniPgRepository<Institution>(ctx), Mock.Of<IAuditLogService>(),
            Mock.Of<ReservEase.Alumni.Paystack.Sdk.Services.IPaystackService>(), new AlumniPgRepository<InstitutionStaff>(ctx), new AlumniPgRepository<Notification>(ctx),
            NullLogger<GroupPayoutService>.Instance), name);
    }

    [Fact]
    public async Task Approving_records_who_approved_and_when()
    {
        var (s, db) = Create();
        var before = DateTime.UtcNow.AddSeconds(-1);

        Assert.Equal(200, (await s.ApproveAsync("g1", "staff1", "Efua Mensah")).Code);

        using var ctx = TestDb.Create(db);
        var g = ctx.Communities.IgnoreQueryFilters().Single(c => c.Id == "g1");
        Assert.Equal(("Approved", "Efua Mensah", null), (g.PayoutStatus, g.PayoutDecidedBy, g.PayoutDecisionNote));
        Assert.True(g.PayoutDecidedAt >= before);
    }

    [Fact]
    public async Task Rejecting_records_who_rejected_when_and_why()
    {
        var (s, db) = Create();

        Assert.Equal(200, (await s.RejectAsync("g2", new RejectGroupPayoutRequest { Notes = "  Name does not match  " }, "staff2", "Kojo Boateng")).Code);

        using (var ctx = TestDb.Create(db))
        {
            var g = ctx.Communities.IgnoreQueryFilters().Single(c => c.Id == "g2");
            Assert.Equal(("Rejected", "Kojo Boateng", "Name does not match"), (g.PayoutStatus, g.PayoutDecidedBy, g.PayoutDecisionNote));
            Assert.NotNull(g.PayoutDecidedAt);
        }
    }
}
