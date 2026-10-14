using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class StructureServiceTests
{
    private const string Tenant = "i1";

    private static MemberEntity Person(string id, int year, string status = "Active")
        => new() { Id = id, InstitutionId = Tenant, FirstName = id.ToUpperInvariant(), LastName = "M", Email = $"{id}@x.com", GraduationYear = year, Status = status };

    private static InstitutionStaff Admin(string id, string role = "ScopedAdmin", int[]? years = null, string[]? communities = null, bool disabled = false)
        => new() { Id = id, InstitutionId = Tenant, FirstName = id, LastName = "A", Email = $"{id}@x.com", Role = role, YearGroups = years?.ToList(), CommunityIds = communities?.ToList(), IsDisabled = disabled };

    private static StructureService Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant))
        {
            db.Add(new PostgresDb.Sdk.Entities.Institution { Id = Tenant, Slug = "umat", Name = "UMaT", PortalName = "UMaT Alumni", Status = "Active", CohortLabel = "Cohort", CohortLabelPlural = "Cohorts" });
            foreach (var e in seed) db.Add(e);
            db.SaveChanges();
        }
        var ctx = TestDb.Create(name, Tenant);
        return new StructureService(
            new AlumniPgRepository<PostgresDb.Sdk.Entities.Institution>(ctx), new AlumniPgRepository<InstitutionStaff>(ctx), new AlumniPgRepository<Batch>(ctx),
            new AlumniPgRepository<Community>(ctx), new AlumniPgRepository<CommunityMembership>(ctx), new AlumniPgRepository<MemberEntity>(ctx),
            TestDb.Tenant(Tenant, "umat"), NullLogger<StructureService>.Instance);
    }

    [Fact]
    public async Task Draws_groups_with_member_counts_and_says_which_have_no_administrator()
    {
        var s = Create(
            new Batch { Id = "b15", InstitutionId = Tenant, Name = "Class of 2015", Year = 2015 },
            new Batch { Id = "b16", InstitutionId = Tenant, Name = "Class of 2016", Year = 2016 },
            Person("a", 2015), Person("b", 2015), Person("c", 2016), Person("d", 2016, status: "Pending"), Person("e", 1999),
            Admin("boss", "SuperAdmin"), Admin("amos", years: [2015]));

        var d = (await s.GetAsync()).Data!;

        Assert.Equal(("UMaT Alumni", "Cohort", "Cohorts"), (d.InstitutionName, d.CohortLabel, d.CohortLabelPlural));
        Assert.Equal([("b16", 1), ("b15", 2)], d.Cohorts.Select(c => (c.Id, c.MemberCount)));   // newest first; pending members are not counted
        Assert.Equal(1, d.MembersWithoutGroup);                                                   // the 1999 graduate has no group yet
        Assert.Equal([1999], d.YearsWithoutGroup);
        Assert.Equal(["b16"], d.UnmanagedCohortIds);                                              // 2015 has Amos; 2016 has nobody
        Assert.Equal(["boss", "amos"], d.Admins.Select(a => a.Id));                               // full administrators first
    }

    [Fact]
    public async Task A_disabled_administrator_looks_after_nothing_and_a_scoped_one_with_no_scope_is_called_out()
    {
        var s = Create(
            new Batch { Id = "b15", InstitutionId = Tenant, Name = "2015", Year = 2015 },
            new Community { Id = "c1", InstitutionId = Tenant, Name = "Engineers" },
            Admin("gone", years: [2015], communities: ["c1"], disabled: true), Admin("idle"));

        var d = (await s.GetAsync()).Data!;

        Assert.Equal(["idle"], d.AdminsWithoutScope);
        Assert.Equal(["b15"], d.UnmanagedCohortIds);
        Assert.Equal(["c1"], d.UnmanagedCommunityIds);
    }

    [Fact]
    public async Task Communities_show_approved_size_pending_requests_and_their_leaders()
    {
        var s = Create(
            new Community { Id = "c1", InstitutionId = Tenant, Name = "Engineers" },
            Person("lead", 2015), Person("m1", 2015), Person("m2", 2016),
            new CommunityMembership { InstitutionId = Tenant, CommunityId = "c1", MemberId = "lead", Status = "Approved", Role = "Leader" },
            new CommunityMembership { InstitutionId = Tenant, CommunityId = "c1", MemberId = "m1", Status = "Approved", Role = "Member" },
            new CommunityMembership { InstitutionId = Tenant, CommunityId = "c1", MemberId = "m2", Status = "Pending", Role = "Member" },
            Admin("amos", communities: ["c1"]));

        var c = (await s.GetAsync()).Data!.Communities.Single();

        Assert.Equal((2, 1), (c.MemberCount, c.PendingCount));
        Assert.Equal("LEAD M", Assert.Single(c.Leaders).Name);
    }
}
