using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

public class InstitutionSwitchServiceTests
{
    private static MemberEntity Person(string id, string inst, string email = "ama@x.com", string status = "Active", bool verified = true)
        => new() { Id = id, InstitutionId = inst, FirstName = "Ama", LastName = "M", Email = email, Status = status, IsEmailVerified = verified };

    private static Institution Inst(string id, string name, string status = "Active")
        => new() { Id = id, Slug = id, Name = name, PortalName = name, Status = status };

    private sealed class Rig
    {
        public required InstitutionSwitchService Service { get; init; }
        public required CurrentTenantService Tenant { get; init; }
    }

    private static Rig Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name)) { foreach (var e in seed) db.Add(e); db.SaveChanges(); }
        var tenant = TestDb.Tenant("a");
        var ctx = TestDb.Create(name, tenant: tenant);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MemberPortalBaseDomain"] = "alumunion.test" }).Build();
        return new Rig
        {
            Tenant = tenant,
            Service = new InstitutionSwitchService(new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<Institution>(ctx),
                new InMemoryRedisService<MemberRedisConfig>(), tenant, config, NullLogger<InstitutionSwitchService>.Instance),
        };
    }

    [Fact]
    public async Task Lists_only_other_active_communities_where_the_same_email_has_an_active_verified_account()
    {
        var rig = Create(Inst("a", "Alpha"), Inst("b", "Bravo"), Inst("c", "Charlie"), Inst("d", "Delta"), Inst("e", "Echo", status: "Suspended"), Inst("f", "Foxtrot"),
            Person("m-a", "a"),
            Person("m-b", "b", email: "AMA@X.com"),                  // same person, different case
            Person("m-c", "c", status: "Pending"),                    // not approved there
            Person("m-d", "d", email: "someone@else.com"),            // someone else
            Person("m-e", "e"),                                       // that community is suspended
            Person("m-f", "f", verified: false));                     // that email was never verified there

        var targets = await rig.Service.ListAsync("m-a");

        var t = Assert.Single(targets);
        Assert.Equal(("b", "Bravo", "https://b.alumunion.test"), (t.InstitutionId, t.Name, t.PortalUrl));
    }

    [Fact]
    public async Task A_member_whose_own_email_is_not_verified_or_who_is_not_active_gets_nothing()
    {
        var unverified = Create(Inst("a", "Alpha"), Inst("b", "Bravo"), Person("m-a", "a", verified: false), Person("m-b", "b"));
        Assert.Empty(await unverified.Service.ListAsync("m-a"));

        var banned = Create(Inst("a", "Alpha"), Inst("b", "Bravo"), Person("m-a", "a", status: "Banned"), Person("m-b", "b"));
        Assert.Empty(await banned.Service.ListAsync("m-a"));
    }

    [Fact]
    public async Task Switching_gives_a_one_time_code_that_works_once_and_only_on_the_target_portal()
    {
        var rig = Create(Inst("a", "Alpha"), Inst("b", "Bravo"), Person("m-a", "a"), Person("m-b", "b"));

        var url = await rig.Service.StartAsync("m-a", "b");

        Assert.StartsWith("https://b.alumunion.test/switch?code=", url);
        var code = url!.Split("code=")[1];

        // Carried to some other community's portal, the code is refused (and is spent).
        rig.Tenant.SetInstitutionId("c");
        Assert.Null(await rig.Service.ConsumeAsync(code));

        // A fresh one, redeemed where it was made for, works exactly once.
        var second = (await rig.Service.StartAsync("m-a", "b"))!.Split("code=")[1];
        rig.Tenant.SetInstitutionId("b");
        var member = await rig.Service.ConsumeAsync(second);
        Assert.Equal("m-b", member!.Id);
        Assert.Null(await rig.Service.ConsumeAsync(second));
    }

    [Fact]
    public async Task A_community_that_is_not_theirs_gives_no_code()
    {
        var rig = Create(Inst("a", "Alpha"), Inst("b", "Bravo"), Inst("c", "Charlie"), Person("m-a", "a"), Person("m-b", "b"), Person("m-c", "c", email: "other@x.com"));

        Assert.Null(await rig.Service.StartAsync("m-a", "c"));
        Assert.Null(await rig.Service.StartAsync("m-a", "nope"));
    }

    [Fact]
    public async Task A_member_banned_after_the_code_was_made_is_refused_on_redeem_and_junk_codes_are_ignored()
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name))
        {
            db.Add(Inst("a", "Alpha")); db.Add(Inst("b", "Bravo")); db.Add(Person("m-a", "a")); db.Add(Person("m-b", "b")); db.SaveChanges();
        }
        var tenant = TestDb.Tenant("a");
        var ctx = TestDb.Create(name, tenant: tenant);
        var redis = new InMemoryRedisService<MemberRedisConfig>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MemberPortalBaseDomain"] = "alumunion.test" }).Build();
        var service = new InstitutionSwitchService(new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<Institution>(ctx), redis, tenant, config,
            NullLogger<InstitutionSwitchService>.Instance);

        var code = (await service.StartAsync("m-a", "b"))!.Split("code=")[1];
        using (var db = TestDb.Create(name)) { db.Set<MemberEntity>().IgnoreQueryFilters().Single(m => m.Id == "m-b").Status = "Banned"; db.SaveChanges(); }
        // A second request: a fresh scope, as in production, so nothing from the first is still tracked.
        var tenantB = TestDb.Tenant("b");
        var redeem = new InstitutionSwitchService(new AlumniPgRepository<MemberEntity>(TestDb.Create(name, tenant: tenantB)),
            new AlumniPgRepository<Institution>(TestDb.Create(name, tenant: tenantB)), redis, tenantB, config, NullLogger<InstitutionSwitchService>.Instance);

        Assert.Null(await redeem.ConsumeAsync(code));
        Assert.Null(await redeem.ConsumeAsync(""));
        Assert.Null(await redeem.ConsumeAsync("short"));
    }
}
