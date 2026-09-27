using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using Xunit;

using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class OnboardingLeadServiceTests
{
    private readonly AlumniDbContext db = new(
        new DbContextOptionsBuilder<AlumniDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        Mock.Of<ICurrentTenantService>());
    private readonly Mock<IAlumniPgRepository<InstitutionEntity>> institutionRepo = new();

    private OnboardingLeadService NewService()
    {
        // Real repository for reads; ExecuteUpdateAsync (unsupported in-memory) is mocked and verified.
        var realInstitutions = new AlumniPgRepository<InstitutionEntity>(db);
        institutionRepo
            .Setup(r => r.GetQueryable(It.IsAny<Expression<Func<InstitutionEntity, bool>>>(), It.IsAny<bool>()))
            .Returns((Expression<Func<InstitutionEntity, bool>>? p, bool i) => realInstitutions.GetQueryable(p, i));
        institutionRepo
            .Setup(r => r.ExecuteUpdateAsync(It.IsAny<Expression<Func<InstitutionEntity, bool>>>(),
                It.IsAny<Expression<Func<SetPropertyCalls<InstitutionEntity>, SetPropertyCalls<InstitutionEntity>>>>(), It.IsAny<bool>()))
            .ReturnsAsync(1);
        return new OnboardingLeadService(
            new AlumniPgRepository<OnboardingLead>(db),
            new AlumniPgRepository<PlatformStaff>(db),
            institutionRepo.Object,
            Mock.Of<IAuditLogService>(),
            NullLogger<OnboardingLeadService>.Instance);
    }

    private static CreateStaffOnboardingLeadRequest Row(string name, string contact = "Contact", string? status = null) =>
        new() { InstitutionName = name, ContactName = contact, Source = "Outreach", Status = status };

    [Fact]
    public async Task ImportAsync_SkipsDuplicatesMissingFieldsAndBadStages()
    {
        db.OnboardingLeads.Add(new OnboardingLead { InstitutionName = "Achimota  OSA", ContactName = "X" });
        db.SaveChanges();

        var result = await NewService().ImportAsync(new ImportOnboardingLeadsRequest
        {
            Rows =
            [
                Row("achimota osa"),                  // duplicate of existing (case/spacing)
                Row("PRESEC OSA", status: "DemoBooked"),
                Row("presec  osa"),                   // duplicate within the file
                Row("Wesley Girls OSA", contact: " "), // missing contact
                Row("Mfantsipim OSA", status: "Approved"), // can't import straight to Approved
                Row("Adisadel OSA"),
            ],
        }, "staff-1", "Staff One");

        Assert.Equal(200, result.Code);
        Assert.Equal(2, result.Data!.Created);
        Assert.Equal(new[] { 1, 3, 4, 5 }, result.Data.Skipped.Select(s => s.Row));
        var presec = db.OnboardingLeads.Single(l => l.InstitutionName == "PRESEC OSA");
        Assert.Equal("DemoBooked", presec.Status);
        Assert.NotNull(presec.ContactedAt);   // skipped stage backfilled
        Assert.NotNull(presec.DemoBookedAt);
        Assert.Equal("staff-1", presec.AssigneeStaffId);
    }

    [Fact]
    public async Task UpdateAsync_NewFollowUpDateResetsReminder_AndRejectsUnknownAssignee()
    {
        var lead = new OnboardingLead { InstitutionName = "OSA", ContactName = "C", NextFollowUpAt = new DateTime(2026, 10, 1), FollowUpReminderSentAt = new DateTime(2026, 10, 1) };
        db.OnboardingLeads.Add(lead);
        db.PlatformStaff.Add(new PlatformStaff { Id = "sales-1", Name = "Sales", Role = "Sales" });
        db.SaveChanges();
        var service = NewService();

        var bad = await service.UpdateAsync(lead.Id, new UpdateOnboardingLeadRequest { InstitutionName = "OSA", ContactName = "C", AssigneeStaffId = "nobody" }, "a", "A");
        Assert.Equal(400, bad.Code);

        var ok = await service.UpdateAsync(lead.Id, new UpdateOnboardingLeadRequest
        {
            InstitutionName = "OSA", ContactName = "C", AssigneeStaffId = "sales-1",
            NextFollowUpAt = new DateTime(2026, 10, 9, 15, 30, 0),
        }, "a", "A");

        Assert.Equal(200, ok.Code);
        Assert.Equal("Sales", ok.Data!.AssigneeName);
        var saved = db.OnboardingLeads.Single();
        Assert.Equal(new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc), saved.NextFollowUpAt);
        Assert.Null(saved.FollowUpReminderSentAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_TrialToApproved_EndsTheInstitutionsTrial_AndClearsFollowUp()
    {
        var lead = new OnboardingLead
        {
            InstitutionName = "OSA", ContactName = "C", Status = OnboardingLeadStatuses.Trial,
            ApprovedInstitutionId = "inst-1", NextFollowUpAt = DateTime.UtcNow,
        };
        db.OnboardingLeads.Add(lead);
        db.SaveChanges();

        var result = await NewService().UpdateStatusAsync(lead.Id, new UpdateOnboardingLeadStatusRequest { Status = OnboardingLeadStatuses.Approved }, "a", "A");

        Assert.Equal(200, result.Code);
        Assert.Null(db.OnboardingLeads.Single().NextFollowUpAt);
        Assert.NotNull(db.OnboardingLeads.Single().ApprovedAt);
        institutionRepo.Verify(r => r.ExecuteUpdateAsync(It.IsAny<Expression<Func<InstitutionEntity, bool>>>(),
            It.IsAny<Expression<Func<SetPropertyCalls<InstitutionEntity>, SetPropertyCalls<InstitutionEntity>>>>(), It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_RejectsUnknownStatus()
    {
        var lead = new OnboardingLead { InstitutionName = "OSA", ContactName = "C" };
        db.OnboardingLeads.Add(lead);
        db.SaveChanges();

        var result = await NewService().UpdateStatusAsync(lead.Id, new UpdateOnboardingLeadStatusRequest { Status = "Maybe" }, "a", "A");

        Assert.Equal(400, result.Code);
    }
}
