using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using Xunit;

using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class DbContextConfigurationTests
{
    [Fact]
    public async Task CampaignServiceGetActiveCampaignsFiltersActiveStatusAndYearGroup()
    {
        var campaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        var memberRepo = new Mock<IAlumniPgRepository<MemberEntity>>();
        var membershipRepo = new Mock<IAlumniPgRepository<CommunityMembership>>();
        var communityRepo = new Mock<IAlumniPgRepository<Community>>();
        var updateRepo = new Mock<IAlumniPgRepository<CampaignUpdate>>();
        var contributionRepo = new Mock<IAlumniPgRepository<Contribution>>();

        var member = new MemberEntity { Id = "member-1", GraduationYear = 2025 };
        memberRepo.Setup(m => m.GetByIdAsync("member-1")).ReturnsAsync(member);
        membershipRepo
            .Setup(m => m.GetAllAsync(It.IsAny<Expression<Func<CommunityMembership, bool>>>()))
            .ReturnsAsync(new List<CommunityMembership>());

        var activeCampaign = new Campaign
        {
            Id = "c1",
            Status = CampaignStatus.Active,
            YearGroups = new List<int> { 2025 }
        };

        var closedCampaign = new Campaign
        {
            Id = "c2",
            Status = CampaignStatus.Closed,
            YearGroups = new List<int> { 2025 }
        };

        var filteredPagedResult = new PgPagedResult<Campaign>
        {
            PageIndex = 1,
            PageSize = 10,
            Count = 1,
            TotalCount = 1,
            TotalPages = 1,
            LowerBoundSize = 1,
            UpperBoundSize = 1,
            Results = new List<Campaign> { activeCampaign }
        };

        campaignRepo
            .Setup(r => r.GetPagedAsync(1, 10, "CreatedAt", "desc", It.IsAny<Expression<Func<Campaign, bool>>>()))
            .ReturnsAsync(filteredPagedResult)
            .Callback<int, int, string, string, Expression<Func<Campaign, bool>>>((p, ps, c, d, filterExpr) =>
            {
                var predicate = filterExpr.Compile();
                Assert.True(predicate(activeCampaign));
                Assert.False(predicate(closedCampaign));
            });

        var campaignService = new CampaignService(campaignRepo.Object, memberRepo.Object, membershipRepo.Object, communityRepo.Object, updateRepo.Object, contributionRepo.Object, new NullLogger<CampaignService>());

        var response = await campaignService.GetActiveCampaignsAsync(new BaseFilter { Page = 1, PageSize = 10 }, "member-1");

        Assert.NotNull(response);
        Assert.Equal(200, response.Code);
        Assert.NotNull(response.Data);
        Assert.Equal(1, response.Data.Count);
        Assert.Equal("c1", response.Data.Results.Single().Id);
    }
}
