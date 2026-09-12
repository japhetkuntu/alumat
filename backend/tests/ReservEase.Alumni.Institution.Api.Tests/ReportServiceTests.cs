using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Institution.Api.Options;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;
using Xunit;

using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class ReportServiceTests
{
    [Fact]
    public async Task ExportEntityCsvAsync_ReturnsBadRequest_ForInvalidEntity()
    {
        var memberRepo = new Mock<IAlumniPgRepository<MemberEntity>>();
        var contributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        var campaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        var eventRepo = new Mock<IAlumniPgRepository<AlumniEvent>>();
        var jobRepo = new Mock<IAlumniPgRepository<Job>>();
        var membershipRepo = new Mock<IAlumniPgRepository<CommunityMembership>>();
        var storeOrderRepo = new Mock<IAlumniPgRepository<StoreOrder>>();
        var serviceRequestRepo = new Mock<IAlumniPgRepository<ServiceRequest>>();
        var service = new ReportService(memberRepo.Object, contributionRepo.Object, campaignRepo.Object, eventRepo.Object, jobRepo.Object, membershipRepo.Object, storeOrderRepo.Object, serviceRequestRepo.Object, new Mock<ICurrentTenantService>().Object, new Mock<IRedisService<InstitutionRedisConfig>>().Object, new NullLogger<ReportService>());

        var result = await service.ExportEntityCsvAsync("unknown", new AuthData { Role = "SuperAdmin" });

        Assert.Equal(400, result.Code);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task ExportEntityCsvAsync_ReturnsCsv_ForCampaigns()
    {
        var memberRepo = new Mock<IAlumniPgRepository<MemberEntity>>();
        var contributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        var campaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        var eventRepo = new Mock<IAlumniPgRepository<AlumniEvent>>();
        var jobRepo = new Mock<IAlumniPgRepository<Job>>();
        var membershipRepo = new Mock<IAlumniPgRepository<CommunityMembership>>();
        var storeOrderRepo = new Mock<IAlumniPgRepository<StoreOrder>>();
        var serviceRequestRepo = new Mock<IAlumniPgRepository<ServiceRequest>>();

        var campaigns = new List<Campaign>
        {
            new Campaign { Id = "c1", Title = "Test", Status = CampaignStatus.Active, TargetAmount = 100, CollectedAmount = 20, PaidCount = 1 }
        };

        campaignRepo.Setup(r => r.GetQueryable(It.IsAny<Expression<Func<Campaign, bool>>>() ) )
            .Returns((Expression<Func<Campaign, bool>>? predicate) =>
                predicate == null
                    ? campaigns.AsQueryable()
                    : campaigns.AsQueryable().Where(predicate.Compile()).AsQueryable());

        var service = new ReportService(memberRepo.Object, contributionRepo.Object, campaignRepo.Object, eventRepo.Object, jobRepo.Object, membershipRepo.Object, storeOrderRepo.Object, serviceRequestRepo.Object, new Mock<ICurrentTenantService>().Object, new Mock<IRedisService<InstitutionRedisConfig>>().Object, new NullLogger<ReportService>());

        var result = await service.ExportEntityCsvAsync("campaigns", new AuthData { Role = "SuperAdmin" });

        Assert.Equal(200, result.Code);
        Assert.NotNull(result.Data);
        Assert.Contains("Id,Title,Status", result.Data!.Content);
        Assert.Contains("Test", result.Data.Content);
    }
}
