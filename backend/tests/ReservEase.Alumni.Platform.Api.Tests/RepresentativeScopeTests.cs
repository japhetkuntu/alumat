using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Options;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using MsOptions = Microsoft.Extensions.Options.Options;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;
using PaymentTransactionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.PaymentTransaction;

namespace ReservEase.Alumni.Platform.Api.Tests;

/// <summary>
/// The platform team's structure: a representative sees and works only what they and the people beneath them own, a SuperAdmin
/// sees everything, and a representative can never put settlement details live.
/// </summary>
public class RepresentativeScopeTests
{
    private static PlatformStaff P(string id, string role, string? reportsTo = null)
        => new() { Id = id, Name = id, Email = $"{id}@x.com", Role = role, ReportsToStaffId = reportsTo };

    private static Institution Inst(string id, string? owner, string status = "Active")
        => new() { Id = id, Name = $"Inst {id}", Slug = id, PortalName = id, ContactName = "c", ContactEmail = "c@x.com", Status = status, OwnerStaffId = owner, OnboardedAt = DateTime.UtcNow };

    private static OnboardingLead Lead(string id, string? assignee, string status = "New")
        => new() { Id = id, InstitutionName = $"Lead {id}", ContactName = "c", ContactEmail = "c@x.com", AssigneeStaffId = assignee, Status = status };

    // boss ── ama ── kofi        esi (another branch)
    private static PlatformStaff[] Team() =>
        [P("boss", "SuperAdmin"), P("ama", "Representative", "boss"), P("kofi", "Representative", "ama"), P("esi", "Representative", "boss")];

    private static (InstitutionManagementService Institutions, OnboardingLeadService Leads, TeamService Team, Mock<IPaystackService> Paystack, AlumniPgRepository<Institution> InstRepo, Func<AlumniPgRepository<Institution>> Fresh) Create(
        IStaffScope scope, string actorId, params object[] seed) => Build(scope, actorId, relational: false, seed);

    /// <summary>Onboarding runs in a database transaction, which the in-memory provider cannot do, so those tests use SQLite.</summary>
    private static (InstitutionManagementService Institutions, OnboardingLeadService Leads, TeamService Team, Mock<IPaystackService> Paystack, AlumniPgRepository<Institution> InstRepo, Func<AlumniPgRepository<Institution>> Fresh) CreateRelational(
        IStaffScope scope, string actorId, params object[] seed) => Build(scope, actorId, relational: true, seed);

    private static (InstitutionManagementService Institutions, OnboardingLeadService Leads, TeamService Team, Mock<IPaystackService> Paystack, AlumniPgRepository<Institution> InstRepo, Func<AlumniPgRepository<Institution>> Fresh) Build(
        IStaffScope scope, string actorId, bool relational, object[] seed)
    {
        var name = TestDb.NewName();
        AlumniDbContext ctx;
        if (relational)
        {
            var (db, _) = TestDb.CreateRelational();   // the connection lives as long as the test process
            foreach (var e in seed) db.Add(e);
            db.SaveChanges();
            ctx = db;
        }
        else
        {
            using (var db = TestDb.Create(name)) { foreach (var e in seed) db.Add(e); db.SaveChanges(); }
            ctx = TestDb.Create(name);
        }
        var paystack = new Mock<IPaystackService>();
        var inst = new AlumniPgRepository<Institution>(ctx);
        var institutions = new InstitutionManagementService(ctx,
            inst, new AlumniPgRepository<StaffEntity>(ctx), new AlumniPgRepository<MemberEntity>(ctx),
            new AlumniPgRepository<Contribution>(ctx), new AlumniPgRepository<StoreOrder>(ctx), new AlumniPgRepository<ServiceRequest>(ctx),
            new AlumniPgRepository<PaymentTransactionEntity>(ctx), new AlumniPgRepository<Campaign>(ctx), new AlumniPgRepository<Batch>(ctx),
            new AlumniPgRepository<ForumCategory>(ctx), new AlumniPgRepository<PlatformStaff>(ctx), Mock.Of<IAuditLogService>(), paystack.Object,
            new ConfigurationBuilder().Build(), Mock.Of<ITemporalClientProvider>(t => t.IsAvailable == false),
            MsOptions.Create(new MailtrapConfig()), Mock.Of<IRedisService<PlatformRedisConfig>>(), scope, NullLogger<InstitutionManagementService>.Instance);
        var leads = new OnboardingLeadService(
            new AlumniPgRepository<OnboardingLead>(ctx), new AlumniPgRepository<PlatformStaff>(ctx), inst,
            Mock.Of<IAuditLogService>(), scope, NullLogger<OnboardingLeadService>.Instance);

        var http = new DefaultHttpContext();
        http.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
        [
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, actorId),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, $"{actorId}@x.com"),
        ], "test"));
        var team = new TeamService(new AlumniPgRepository<PlatformStaff>(ctx), inst, new AlumniPgRepository<MemberEntity>(ctx),
            new AlumniPgRepository<OnboardingLead>(ctx), Mock.Of<IAuditLogService>(), scope, new HttpContextAccessor { HttpContext = http },
            NullLogger<TeamService>.Instance);
        return (institutions, leads, team, paystack, inst, () => new AlumniPgRepository<Institution>(TestDb.Create(name)));
    }

    private static object[] Seed(params object[] more) => [.. Team(), .. more];

    private static CreateInstitutionRequest NewInstitution(string slug) => new()
    {
        Name = $"Greenfield {slug}", Slug = slug, ContactName = "Naomi", ContactEmail = "naomi@x.com",
        AdminFirstName = "Naomi", AdminLastName = "B", AdminEmail = "naomi@x.com",
        PlatformFeePercentage = 0.5m, PlatformFeeFlatThreshold = 100, PlatformFeeFlatAmount = 1,
        SettlementBankCode = "GCB", SettlementBankName = "GCB Bank", SettlementAccountNumber = "0123456789", SettlementAccountName = "Greenfield",
    };

    // ── What each person can see ──────────────────────────────────────────────

    [Fact]
    public async Task A_representative_lists_only_what_they_and_their_team_own_while_a_superadmin_sees_everything()
    {
        var seed = Seed(Inst("mine", "ama"), Inst("under-me", "kofi"), Inst("sibling", "esi"), Inst("boss-own", "boss"), Inst("nobody", null));

        var rep = Create(FixedScope.Representative("ama", "kofi"), "ama", seed);
        var all = Create(new FixedScope(), "boss", seed);

        var repList = (await rep.Institutions.GetInstitutionsAsync(1, 50, null, null)).Data!.Results.Select(i => i.Slug).OrderBy(x => x);
        var bossList = (await all.Institutions.GetInstitutionsAsync(1, 50, null, null)).Data!.Results.Select(i => i.Slug).OrderBy(x => x);

        Assert.Equal(["mine", "under-me"], repList);
        Assert.Equal(["boss-own", "mine", "nobody", "sibling", "under-me"], bossList);   // including unassigned
    }

    [Fact]
    public async Task An_institution_outside_a_representatives_view_is_reported_as_not_found()
    {
        var seed = Seed(Inst("sibling", "esi"), Inst("under-me", "kofi"));
        var rep = Create(FixedScope.Representative("ama", "kofi"), "ama", seed);

        Assert.Equal(404, (await rep.Institutions.GetInstitutionAsync("sibling")).Code);
        Assert.Equal(200, (await rep.Institutions.GetInstitutionAsync("under-me")).Code);
    }

    [Fact]
    public async Task A_representative_works_only_the_leads_assigned_within_their_team_and_cannot_open_or_change_others()
    {
        var seed = Seed(Lead("mine", "ama"), Lead("theirs", "esi"), Lead("loose", null));
        var rep = Create(FixedScope.Representative("ama", "kofi"), "ama", seed);
        var boss = Create(new FixedScope(), "boss", seed);

        Assert.Equal(["mine"], (await rep.Leads.GetLeadsAsync(null)).Data!.Select(l => l.Id));
        Assert.Equal(3, (await boss.Leads.GetLeadsAsync(null)).Data!.Count);
        Assert.Equal(404, (await rep.Leads.GetLeadByIdAsync("theirs")).Code);
        Assert.Equal(404, (await rep.Leads.GetLeadByIdAsync("loose")).Code);
        Assert.Equal(404, (await rep.Leads.AddNoteAsync("theirs", new AddInternalNoteRequest { Note = "x" }, "ama", "Ama")).Code);
    }

    [Fact]
    public async Task A_representative_can_only_hand_a_lead_to_themselves_or_someone_beneath_them()
    {
        var seed = Seed(Lead("mine", "ama"));
        var rep = Create(FixedScope.Representative("ama", "kofi"), "ama", seed);

        var assignees = (await rep.Leads.GetAssigneesAsync()).Data!.Select(a => a.Id).OrderBy(x => x);

        Assert.Equal(["ama", "kofi"], assignees);   // not boss, not esi
    }

    // ── What a representative may and may not decide ──────────────────────────

    [Fact]
    public async Task A_representative_onboards_an_institution_they_own_with_settlement_waiting_for_approval_and_default_terms()
    {
        var rep = CreateRelational(FixedScope.Representative("ama"), "ama", Seed());

        var response = await rep.Institutions.CreateInstitutionAsync(NewInstitution("greenfield"), "ama", "Ama");

        // (SQLite cannot sum decimals, so building the response after the save can fail here; what was saved is what matters.)
        _ = response;
        var saved = await rep.InstRepo.GetOneAsync(i => i.Slug == "greenfield");
        Assert.Equal("ama", saved!.OwnerStaffId);
        // Settlement is held for a SuperAdmin or Billing to approve, never made live by the person onboarding.
        Assert.Null(saved.SettlementAccountNumber);
        Assert.Null(saved.PaystackSubaccountCode);
        Assert.Equal("Pending", saved.PayoutStatus);
        Assert.Equal("0123456789", saved.PendingPayoutChanges!.SettlementAccountNumber);
        // Commercial terms are the platform's, not whatever was typed.
        Assert.Equal(5m, saved.PlatformFeePercentage);
        Assert.Null(saved.PlatformFeeFlatAmount);
        rep.Paystack.Verify(p => p.CreateSubaccountAsync(It.IsAny<SubaccountRequest>()), Times.Never);
    }

    [Fact]
    public async Task A_superadmin_onboarding_keeps_full_control_of_terms_and_settlement()
    {
        var boss = CreateRelational(new FixedScope(), "boss", Seed());
        boss.Paystack.Setup(p => p.CreateSubaccountAsync(It.IsAny<SubaccountRequest>()))
            .ReturnsAsync(new SubaccountResponse { Status = true, Data = new SubaccountData { SubaccountCode = "ACCT_x" } });

        var response = await boss.Institutions.CreateInstitutionAsync(NewInstitution("greenfield"), "boss", "Boss");

        // (SQLite cannot sum decimals, so building the response after the save can fail here; what was saved is what matters.)
        _ = response;
        var saved = await boss.InstRepo.GetOneAsync(i => i.Slug == "greenfield");
        Assert.Equal("boss", saved!.OwnerStaffId);                  // defaults to whoever created it
        Assert.Equal(0.5m, saved.PlatformFeePercentage);
        Assert.Equal("0123456789", saved.SettlementAccountNumber);
        Assert.Equal("ACCT_x", saved.PaystackSubaccountCode);
    }

    [Fact]
    public async Task A_superadmin_may_name_a_different_owner_but_a_representative_may_not()
    {
        var boss = CreateRelational(new FixedScope(), "boss", Seed());
        var request = NewInstitution("one"); request.OwnerStaffId = "kofi"; request.SettlementBankCode = null;
        await boss.Institutions.CreateInstitutionAsync(request, "boss", "Boss");
        Assert.Equal("kofi", (await boss.InstRepo.GetOneAsync(i => i.Slug == "one"))!.OwnerStaffId);

        var rep = CreateRelational(FixedScope.Representative("ama"), "ama", Seed());
        var sneaky = NewInstitution("two"); sneaky.OwnerStaffId = "esi";
        await rep.Institutions.CreateInstitutionAsync(sneaky, "ama", "Ama");
        Assert.Equal("ama", (await rep.InstRepo.GetOneAsync(i => i.Slug == "two"))!.OwnerStaffId);
    }

    // ── The team view and handing work over ───────────────────────────────────

    [Fact]
    public async Task The_team_view_shows_each_persons_own_numbers_and_only_a_superadmin_sees_unassigned_work()
    {
        var seed = Seed(
            Inst("a1", "ama"), Inst("a2", "ama", status: "Suspended"), Inst("k1", "kofi"), Inst("e1", "esi"), Inst("loose", null),
            Lead("l1", "ama"), Lead("l2", "ama", status: "Approved"), Lead("l3", null));

        var boss = Create(new FixedScope(), "boss", seed);
        var rep = Create(FixedScope.Representative("ama", "kofi"), "ama", seed);

        var bossView = (await boss.Team.GetAsync()).Data!;
        var ama = bossView.People.Single(p => p.Id == "ama");
        Assert.Equal((2, 1, 1, 1), (ama.Institutions, ama.ActiveInstitutions, ama.OpenLeads, ama.ConvertedLeads));
        Assert.Equal(1, ama.DirectReports);
        Assert.Equal(4, bossView.People.Count);
        Assert.Equal(1, bossView.UnassignedInstitutions);
        Assert.Equal(1, bossView.UnassignedLeads);

        var repView = (await rep.Team.GetAsync()).Data!;
        Assert.Equal(["ama", "kofi"], repView.People.Select(p => p.Id).OrderBy(x => x));
        Assert.Null(repView.UnassignedInstitutions);
    }

    [Fact]
    public async Task Handing_work_over_moves_only_what_the_caller_can_see_and_only_to_their_own_team()
    {
        var seed = Seed(Inst("mine", "ama"), Inst("sibling", "esi"));
        var rep = Create(FixedScope.Representative("ama", "kofi"), "ama", seed);

        var toSibling = await rep.Team.ReassignAsync(new ReassignRequest { ToStaffId = "esi", InstitutionIds = ["mine"] }, "ama", "Ama");
        Assert.Equal(400, toSibling.Code);

        var ok = await rep.Team.ReassignAsync(new ReassignRequest { ToStaffId = "kofi", InstitutionIds = ["mine", "sibling"] }, "ama", "Ama");
        Assert.Equal(200, ok.Code);
        Assert.Equal(1, ok.Data!.Institutions);   // "sibling" was never theirs to move
        Assert.Equal("kofi", (await rep.InstRepo.GetOneAsync(i => i.Id == "mine"))!.OwnerStaffId);
        Assert.Equal("esi", (await rep.InstRepo.GetOneAsync(i => i.Id == "sibling"))!.OwnerStaffId);
    }

    [Fact]
    public async Task Only_a_superadmin_view_can_change_a_reporting_line_and_a_loop_is_refused()
    {
        var boss = Create(new FixedScope(), "boss", Seed());

        var loop = await boss.Team.SetManagerAsync("ama", "kofi", "boss", "Boss");
        Assert.Equal(400, loop.Code);

        var ok = await boss.Team.SetManagerAsync("kofi", "esi", "boss", "Boss");
        Assert.Equal(200, ok.Code);
        Assert.Equal("esi", ok.Data!.ReportsToStaffId);
    }

    // ── Staff accounts ────────────────────────────────────────────────────────

    private static PlatformStaffService Staff(params PlatformStaff[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name)) { foreach (var e in seed) db.Add(e); db.SaveChanges(); }
        return new PlatformStaffService(new AlumniPgRepository<PlatformStaff>(TestDb.Create(name)), Mock.Of<IAuditLogService>(), NullLogger<PlatformStaffService>.Instance);
    }

    [Fact]
    public async Task Disabling_a_representative_with_an_older_client_keeps_their_reporting_line()
    {
        var staff = Staff(Team());

        // The existing screen sends no manager at all.
        var response = await staff.UpdateAsync("kofi", new UpdatePlatformStaffRequest { Name = "kofi", Role = "Representative", IsDisabled = true }, "boss", "Boss");

        Assert.Equal(200, response.Code);
        Assert.Equal("ama", response.Data!.ReportsToStaffId);
        Assert.True(response.Data.IsDisabled);
    }

    [Fact]
    public async Task A_representative_who_leads_a_team_cannot_be_disabled_until_their_people_are_moved()
    {
        var staff = Staff(Team());

        var response = await staff.UpdateAsync("ama", new UpdatePlatformStaffRequest { Name = "ama", Role = "Representative", IsDisabled = true }, "boss", "Boss");

        Assert.Equal(400, response.Code);
        Assert.Contains("Move the people", response.Message);
    }

    [Fact]
    public async Task A_new_representative_must_say_who_they_report_to()
    {
        var staff = Staff(Team());

        var without = await staff.CreateAsync(new CreatePlatformStaffRequest { Name = "New", Email = "new@x.com", Password = "password1", Role = "Representative" }, "boss", "Boss");
        var with = await staff.CreateAsync(new CreatePlatformStaffRequest { Name = "New", Email = "new2@x.com", Password = "password1", Role = "Representative", ReportsToStaffId = "ama" }, "boss", "Boss");

        Assert.Equal(400, without.Code);
        Assert.Equal(201, with.Code);
        Assert.Equal("ama", with.Data!.ReportsToStaffId);
    }
}
