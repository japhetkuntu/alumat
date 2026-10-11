using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

/// <summary>Posting to the year-group wall follows the institution's own rule about whether dues are required.</summary>
public class ClassNoteDuesPolicyTests
{
    private const string Tenant = "inst-1";

    private static (ClassNoteService service, ReservEase.Alumni.Common.Sdk.Models.AuthData who) Create(string policy)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant))
        {
            db.Add(new Institution { Id = Tenant, Slug = "umat", Name = "UMaT", Status = "Active", MemberActivePolicy = policy });
            db.Add(new MemberEntity { Id = "m1", InstitutionId = Tenant, FirstName = "Ama", LastName = "M", Email = "a@x.com", Status = "Active", GraduationYear = 2015 });
            // A dues campaign is open for the member's years and they have not paid it.
            db.Add(new Campaign { Id = "dues", InstitutionId = Tenant, Title = "Dues 2026", IsMembershipCampaign = true, MembershipYear = 2026 });
            db.SaveChanges();
        }
        var tenant = TestDb.Tenant(Tenant, "umat");
        var ctx = TestDb.Create(name, Tenant);
        var service = new ClassNoteService(
            new AlumniPgRepository<ClassNote>(ctx), new AlumniPgRepository<ClassNoteLike>(ctx), new AlumniPgRepository<MemberEntity>(ctx),
            new AlumniPgRepository<Campaign>(ctx), new AlumniPgRepository<Contribution>(ctx), new AlumniPgRepository<CommunityMembership>(ctx),
            new AlumniPgRepository<Institution>(ctx), Mock.Of<ITemporalClientProvider>(t => t.IsAvailable == false), tenant, NullLogger<ClassNoteService>.Instance);
        return (service, new() { Id = "m1", FirstName = "Ama", LastName = "M", Email = "a@x.com", Role = "Member" });
    }

    [Fact]
    public async Task Where_dues_are_not_required_an_approved_member_can_post_without_having_paid()
    {
        var (s, who) = Create(MembershipActivityCalculator.ApprovedOnlyPolicy);

        var r = await s.CreateClassNoteAsync(new CreateClassNoteRequest("Reunion on Saturday", null), who);

        Assert.Equal(201, r.Code);
    }

    [Fact]
    public async Task Where_dues_are_required_an_unpaid_member_still_cannot_post()
    {
        var (s, who) = Create(MembershipActivityCalculator.DuesRequiredPolicy);

        var r = await s.CreateClassNoteAsync(new CreateClassNoteRequest("Reunion on Saturday", null), who);

        Assert.Equal(400, r.Code);
        Assert.Contains("Active membership required", r.Message);
    }

    [Fact]
    public async Task Where_dues_are_not_required_the_wall_can_be_read_too()
    {
        var (s, who) = Create(MembershipActivityCalculator.ApprovedOnlyPolicy);

        var r = await s.GetClassNotesAsync(1, 20, who);

        Assert.Equal(200, r.Code);
    }
}
