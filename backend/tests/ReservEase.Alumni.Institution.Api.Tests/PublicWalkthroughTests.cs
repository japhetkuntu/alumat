using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Moq;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Member.Api.Controllers;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class PublicWalkthroughTests
{
    private readonly Mock<IAlumniPgRepository<OnboardingLead>> leads = new();
    private readonly Mock<IAlumniPgRepository<PlatformStaff>> staff = new();
    private readonly Mock<IAlumniPgRepository<PlatformNotification>> notifications = new();
    private OnboardingLead? saved;

    private PublicController Controller(IAlumniPgRepository<OnboardingLead>? leadStore = null)
    {
        leads.Setup(r => r.AddAsync(It.IsAny<OnboardingLead>()))
            .Callback<OnboardingLead>(l => saved = l).ReturnsAsync(1);
        staff.Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<PlatformStaff, bool>>>(), false))
            .ReturnsAsync((Expression<Func<PlatformStaff, bool>> predicate, bool _) => new[]
            {
                new PlatformStaff { Id = "admin-1", Role = "SuperAdmin" },
                new PlatformStaff { Id = "sales-1", Role = "Sales" },
                new PlatformStaff { Id = "support-1", Role = "Support" },
                new PlatformStaff { Id = "disabled-admin", Role = "SuperAdmin", IsDisabled = true },
            }.Where(predicate.Compile()).ToList());
        notifications.Setup(r => r.AddRangeAsync(It.IsAny<List<PlatformNotification>>())).ReturnsAsync(1);
        var constructor = typeof(PublicController).GetConstructors().Single();
        var emptyMock = typeof(Mock).GetMethods().Single(m => m.Name == "Of" && m.GetParameters().Length == 0);
        var args = constructor.GetParameters().Select(p =>
            p.ParameterType == typeof(IAlumniPgRepository<OnboardingLead>) ? leadStore ?? leads.Object :
            p.ParameterType == typeof(IAlumniPgRepository<PlatformStaff>) ? (object)staff.Object :
            p.ParameterType == typeof(IAlumniPgRepository<PlatformNotification>) ? notifications.Object :
            emptyMock.MakeGenericMethod(p.ParameterType).Invoke(null, null)).ToArray();
        var controller = (PublicController)constructor.Invoke(args);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
        return controller;
    }

    [Fact]
    public async Task Walkthrough_SavesEnquiryWithoutInventingAgreementAcceptance()
    {
        var result = await Controller().CreateWalkthrough(new CreateWalkthroughRequest
        {
            InstitutionName = " Example Association ", ContactName = " Contact Person ",
            ContactEmail = " contact@example.org ", MainInterest = " Events and RSVPs ",
        });
        Assert.IsType<CreatedResult>(result);
        Assert.NotNull(saved);
        Assert.Equal("Example Association", saved.InstitutionName);
        Assert.Equal("Contact Person", saved.ContactName);
        Assert.Equal("contact@example.org", saved.ContactEmail);
        Assert.Equal("Website walkthrough", saved.Source);
        Assert.Equal("New", saved.Status);
        Assert.Equal(new[] { "Events and RSVPs" }, saved.PrimaryGoals);
        Assert.Null(saved.AgreementVersion);
        Assert.Null(saved.AgreementAcceptedAt);
        Assert.Null(saved.AgreementAcceptedByName);
        Assert.Null(saved.AgreementAcceptedByTitle);
        Assert.Null(saved.AgreementAcceptedIp);
        notifications.Verify(r => r.AddRangeAsync(It.Is<List<PlatformNotification>>(items =>
            items.Count == 2 && items.All(n => n.Title == "New walkthrough request" && n.ActionUrl == "/onboarding-leads") && items.Any(n => n.RecipientStaffId == "admin-1") && items.Any(n => n.RecipientStaffId == "sales-1"))), Times.Once);
    }

    [Fact]
    public async Task Walkthrough_IsVisibleInPlatformRequestsWithContactAndInterest()
    {
        await using var db = new AlumniDbContext(
            new DbContextOptionsBuilder<AlumniDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Mock.Of<ICurrentTenantService>());
        var leadStore = new AlumniPgRepository<OnboardingLead>(db);
        await Controller(leadStore).CreateWalkthrough(new CreateWalkthroughRequest
        {
            InstitutionName = "Example Association", ContactName = "Demo Contact",
            ContactEmail = "demo@example.org", MainInterest = "Jobs and mentorship",
        });
        var platform = new OnboardingLeadService(leadStore,
            new AlumniPgRepository<PlatformStaff>(db), new AlumniPgRepository<ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution>(db),
            Mock.Of<IAuditLogService>(), NullLogger<OnboardingLeadService>.Instance);
        var result = await platform.GetLeadsAsync("New");
        Assert.Equal(200, result.Code);
        var lead = Assert.Single(result.Data!);
        Assert.Equal("Website walkthrough", lead.Source);
        Assert.Equal("Demo Contact", lead.ContactName);
        Assert.Equal("demo@example.org", lead.ContactEmail);
        Assert.Equal(new[] { "Jobs and mentorship" }, lead.PrimaryGoals);
        Assert.Null(lead.AgreementAcceptedAt);
        var detail = await platform.GetLeadByIdAsync(lead.Id);
        Assert.Equal(200, detail.Code);
        Assert.Equal(lead.ContactEmail, detail.Data!.ContactEmail);
    }

    [Fact]
    public async Task Onboarding_StillRequiresCurrentAgreement()
    {
        await Controller().CreateOnboardingLead(new CreateOnboardingLeadRequest
        {
            InstitutionName = "Example", ContactName = "Contact", ContactEmail = "contact@example.org",
            AgreementAccepted = false,
        });
        leads.Verify(r => r.AddAsync(It.IsAny<OnboardingLead>()), Times.Never);
    }

    [Fact]
    public async Task Onboarding_StillRecordsExplicitAcceptance()
    {
        await Controller().CreateOnboardingLead(new CreateOnboardingLeadRequest
        {
            InstitutionName = "Example", ContactName = "Contact", ContactEmail = "contact@example.org",
            ContactRole = "Chairperson", AgreementAccepted = true, AgreementVersion = InstitutionAgreement.CurrentVersion,
        });
        Assert.NotNull(saved);
        Assert.Equal(InstitutionAgreement.CurrentVersion, saved.AgreementVersion);
        Assert.NotNull(saved.AgreementAcceptedAt);
        Assert.Equal("Contact", saved.AgreementAcceptedByName);
        Assert.Equal("Chairperson", saved.AgreementAcceptedByTitle);
        Assert.Equal("127.0.0.1", saved.AgreementAcceptedIp);
    }

    [Theory]
    [InlineData("", "Contact", "contact@example.org")]
    [InlineData("Example", " ", "contact@example.org")]
    [InlineData("Example", "Contact", "invalid-email")]
    public void Walkthrough_RejectsMissingOrInvalidContactFields(string organisation, string name, string email)
    {
        var request = new CreateWalkthroughRequest { InstitutionName = organisation, ContactName = name, ContactEmail = email };
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), [], validateAllProperties: true));
    }

    [Fact]
    public void Walkthrough_UsesTheExistingSubmissionRateLimit()
    {
        var attribute = typeof(PublicController).GetMethod(nameof(PublicController.CreateWalkthrough))!
            .GetCustomAttributes(typeof(EnableRateLimitingAttribute), false).Cast<EnableRateLimitingAttribute>().Single();
        Assert.Equal(RateLimitingExtensions.AuthPolicy, attribute.PolicyName);
    }
}
