using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>Adding a staff member never involves typing a password for them: they are emailed a link to choose their own.</summary>
public class InstitutionStaffInviteTests
{
    private const string Tenant = "inst-1";
    private static readonly AuthData Boss = new() { Id = "boss", Role = "SuperAdmin" };

    private static (InstitutionStaffService Service, Mock<IInstitutionAuthService> Auth, string Db) Rig()
    {
        var db = TestDb.NewName();
        var auth = new Mock<IInstitutionAuthService>();
        var service = new InstitutionStaffService(new AlumniPgRepository<StaffEntity>(TestDb.Create(db, Tenant)), auth.Object,
            Mock.Of<IInstitutionAuditLogService>(), NullLogger<InstitutionStaffService>.Instance);
        return (service, auth, db);
    }

    [Fact]
    public async Task AddingAnAdmin_EmailsThemASetPasswordLink_AndStoresNoUsablePassword()
    {
        var (service, auth, dbName) = Rig();
        var result = await service.CreateInstitutionStaffAsync(new CreateInstitutionStaffRequest
        {
            FirstName = "Ama", LastName = "Mensah", Email = "Ama@Example.org", Role = "SuperAdmin",
        }, Boss);

        Assert.Equal(201, result.Code);
        Assert.True(result.Data!.InvitePending);
        auth.Verify(a => a.SendPasswordSetupLinkAsync(It.Is<StaffEntity>(s => s.Email == "ama@example.org")), Times.Once);
        using var check = TestDb.Create(dbName, Tenant);
        var saved = check.InstitutionStaff.Single();
        Assert.False(BCrypt.Net.BCrypt.Verify("", saved.Password));
        Assert.Null(saved.LastLoginAt);
    }

    [Fact]
    public async Task ResendingAnInvite_OnlyWorksUntilThePersonHasSignedIn()
    {
        var (service, auth, dbName) = Rig();
        using (var seed = TestDb.Create(dbName, Tenant))
        {
            seed.InstitutionStaff.Add(new StaffEntity { Id = "new", InstitutionId = Tenant, FirstName = "A", LastName = "B", Email = "new@x.org", Password = "x", Role = "SuperAdmin" });
            seed.InstitutionStaff.Add(new StaffEntity { Id = "old", InstitutionId = Tenant, FirstName = "C", LastName = "D", Email = "old@x.org", Password = "x", Role = "SuperAdmin", LastLoginAt = DateTime.UtcNow });
            seed.SaveChanges();
        }
        var service2 = new InstitutionStaffService(new AlumniPgRepository<StaffEntity>(TestDb.Create(dbName, Tenant)), auth.Object,
            Mock.Of<IInstitutionAuditLogService>(), NullLogger<InstitutionStaffService>.Instance);

        Assert.Equal(200, (await service2.ResendInviteAsync("new", Boss)).Code);
        Assert.Equal(400, (await service2.ResendInviteAsync("old", Boss)).Code);
        auth.Verify(a => a.SendPasswordSetupLinkAsync(It.IsAny<StaffEntity>()), Times.Once);
    }
}
