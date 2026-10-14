using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using Xunit;

namespace ReservEase.Alumni.Platform.Api.Tests;

/// <summary>Settlement to representatives is entered by hand: the ledger records earnings and payments and never moves money.</summary>
public class IncentiveServiceTests
{
    private static PlatformStaff P(string id, string role) => new() { Id = id, Name = id, Email = $"{id}@x.com", Role = role };

    private static IncentiveService Create(IStaffScope scope, string callerId, params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name)) { foreach (var e in seed) db.Add(e); db.SaveChanges(); }
        var ctx = TestDb.Create(name);
        var ctxHttp = new DefaultHttpContext();
        ctxHttp.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, callerId),
            new Claim(ClaimTypes.Email, $"{callerId}@x.com"),
        ], "test"));
        var http = new HttpContextAccessor { HttpContext = ctxHttp };
        return new IncentiveService(
            new AlumniPgRepository<RepresentativeIncentiveEntry>(ctx), new AlumniPgRepository<PlatformStaff>(ctx),
            new AlumniPgRepository<Institution>(ctx), Mock.Of<IAuditLogService>(), scope, http,
            NullLogger<IncentiveService>.Instance);
    }

    private static AddIncentiveEntryRequest Req(string staff, string kind, decimal amount, string reason = "r")
        => new() { StaffId = staff, Kind = kind, Amount = amount, Reason = reason };

    [Fact]
    public async Task Owed_is_earned_minus_paid_and_voided_lines_do_not_count()
    {
        var svc = Create(FixedScope.Everything(), "boss", P("boss", "SuperAdmin"), P("ama", "Representative"));
        await svc.AddAsync(Req("ama", "Earned", 500), "boss", "Boss");
        var second = await svc.AddAsync(Req("ama", "Earned", 200), "boss", "Boss");
        await svc.AddAsync(Req("ama", "Paid", 300), "boss", "Boss");
        await svc.VoidAsync(second.Data!.Id, "entered twice", "boss", "Boss");

        var b = Assert.Single((await svc.GetAsync()).Data!.Balances, x => x.StaffId == "ama");
        Assert.Equal(500, b.Earned);
        Assert.Equal(300, b.Paid);
        Assert.Equal(200, b.Owed);
    }

    [Fact]
    public async Task A_payment_larger_than_what_is_owed_is_refused()
    {
        var svc = Create(FixedScope.Everything(), "boss", P("boss", "SuperAdmin"), P("ama", "Representative"));
        await svc.AddAsync(Req("ama", "Earned", 100), "boss", "Boss");
        var tooMuch = await svc.AddAsync(Req("ama", "Paid", 150), "boss", "Boss");
        Assert.False(tooMuch.Code is >= 200 and < 300);
        var exact = await svc.AddAsync(Req("ama", "Paid", 100), "boss", "Boss");
        Assert.True(exact.Code is >= 200 and < 300);
    }

    [Fact]
    public async Task Only_a_representative_can_be_credited()
    {
        var svc = Create(FixedScope.Everything(), "boss", P("boss", "SuperAdmin"), P("sup", "Support"));
        Assert.Equal(400, (await svc.AddAsync(Req("sup", "Earned", 50), "boss", "Boss")).Code);
        Assert.Equal(400, (await svc.AddAsync(Req("ghost", "Earned", 50), "boss", "Boss")).Code);
    }

    [Fact]
    public async Task A_representative_sees_only_their_own_ledger()
    {
        var seed = new object[]
        {
            P("boss", "SuperAdmin"), P("ama", "Representative"), P("kofi", "Representative"),
            new RepresentativeIncentiveEntry { StaffId = "ama", Kind = "Earned", Amount = 400, Reason = "a", RecordedByStaffId = "boss", RecordedByName = "Boss" },
            new RepresentativeIncentiveEntry { StaffId = "kofi", Kind = "Earned", Amount = 900, Reason = "k", RecordedByStaffId = "boss", RecordedByName = "Boss" },
        };
        // Ama leads Kofi, yet still sees only her own lines.
        var asAma = await Create(FixedScope.Representative("ama", "kofi"), "ama", seed).GetAsync();
        Assert.False(asAma.Data!.CanRecord);
        Assert.All(asAma.Data.Entries, e => Assert.Equal("ama", e.StaffId));
        Assert.Single(asAma.Data.Balances);
        Assert.Equal(400, asAma.Data.Balances[0].Owed);

        var asBoss = await Create(FixedScope.Everything(), "boss", seed).GetAsync();
        Assert.True(asBoss.Data!.CanRecord);
        Assert.Equal(2, asBoss.Data.Entries.Count);
    }

    [Fact]
    public async Task Voiding_an_earning_that_payments_depend_on_is_refused()
    {
        var svc = Create(FixedScope.Everything(), "boss", P("boss", "SuperAdmin"), P("ama", "Representative"));
        var earned = await svc.AddAsync(Req("ama", "Earned", 100), "boss", "Boss");
        await svc.AddAsync(Req("ama", "Paid", 100), "boss", "Boss");
        var result = await svc.VoidAsync(earned.Data!.Id, "wrong", "boss", "Boss");
        Assert.False(result.Code is >= 200 and < 300);
    }
}
