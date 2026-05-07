using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umat.Alumni.Admin.Api.Services.Implementations;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Repositories;
using Xunit;

using MemberEntity = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace Umat.Alumni.Admin.Api.Tests;

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
        var service = new ReportService(memberRepo.Object, contributionRepo.Object, campaignRepo.Object, eventRepo.Object, jobRepo.Object, new NullLogger<ReportService>());

        var result = await service.ExportEntityCsvAsync("unknown", new AuthData { Role = "Admin" });

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

        var campaigns = new List<Campaign>
        {
            new Campaign { Id = "c1", Title = "Test", Status = CampaignStatus.Active, TargetAmount = 100, CollectedAmount = 20, PaidCount = 1 }
        };

        campaignRepo.Setup(r => r.GetQueryable(It.IsAny<Expression<Func<Campaign, bool>>>() ) )
            .Returns((Expression<Func<Campaign, bool>>? predicate) =>
                predicate == null
                    ? campaigns.AsQueryable()
                    : campaigns.AsQueryable().Where(predicate.Compile()).AsQueryable());

        var service = new ReportService(memberRepo.Object, contributionRepo.Object, campaignRepo.Object, eventRepo.Object, jobRepo.Object, new NullLogger<ReportService>());

        var result = await service.ExportEntityCsvAsync("campaigns", new AuthData { Role = "SuperAdmin" });

        Assert.Equal(200, result.Code);
        Assert.NotNull(result.Data);
        Assert.Contains("Id,Title,Status", result.Data!.Content);
        Assert.Contains("Test", result.Data.Content);
    }
}
