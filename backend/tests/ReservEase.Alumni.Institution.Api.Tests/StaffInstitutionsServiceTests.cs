using Microsoft.Extensions.Configuration;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class StaffInstitutionsServiceTests
{
    private static PostgresDb.Sdk.Entities.Institution Inst(string id, string name, string status = "Active")
        => new() { Id = id, Slug = id, Name = name, PortalName = name, Status = status };

    private static StaffEntity Staff(string id, string inst, string email = "ama@x.com", bool disabled = false)
        => new() { Id = id, InstitutionId = inst, FirstName = "Ama", LastName = "A", Email = email, IsDisabled = disabled, Role = "SuperAdmin" };

    private static StaffInstitutionsService Create(string? adminDomain, params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name)) { foreach (var e in seed) db.Add(e); db.SaveChanges(); }
        var ctx = TestDb.Create(name, "a");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AdminPortalBaseDomain"] = adminDomain }).Build();
        return new StaffInstitutionsService(new AlumniPgRepository<StaffEntity>(ctx), new AlumniPgRepository<PostgresDb.Sdk.Entities.Institution>(ctx), config);
    }

    [Fact]
    public async Task Lists_other_active_institutions_where_the_same_email_administers_and_links_to_their_sign_in_with_the_email_filled_in()
    {
        var s = Create("admin.alumunion.test", Inst("a", "Alpha"), Inst("b", "Bravo"), Inst("c", "Charlie"), Inst("d", "Delta", status: "Suspended"),
            Staff("s-a", "a"), Staff("s-b", "b", email: "AMA@x.com"), Staff("s-c", "c", disabled: true), Staff("s-d", "d"));

        var list = await s.ListAsync("s-a");

        var b = Assert.Single(list);
        Assert.Equal(("Bravo", "https://b.admin.alumunion.test/login?email=ama%40x.com"), (b.Name, b.LoginUrl));
    }

    [Fact]
    public async Task Without_an_admin_domain_or_for_a_disabled_account_there_is_nothing_to_list()
    {
        Assert.Empty(await Create(null, Inst("a", "Alpha"), Inst("b", "Bravo"), Staff("s-a", "a"), Staff("s-b", "b")).ListAsync("s-a"));
        Assert.Empty(await Create("admin.x.test", Inst("a", "Alpha"), Inst("b", "Bravo"), Staff("s-a", "a", disabled: true), Staff("s-b", "b")).ListAsync("s-a"));
    }
}
