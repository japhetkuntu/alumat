using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umat.Alumni.Admin.Api.Extensions;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Admin.Api.Services.Implementations;
using AdminContributionService = Umat.Alumni.Admin.Api.Services.Implementations.ContributionService;
using AdminCampaignService = Umat.Alumni.Admin.Api.Services.Implementations.CampaignService;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Repositories;
using Umat.Alumni.Storage.Sdk.Services;
using Umat.Alumni.Redis.Sdk.Services;
using Microsoft.Extensions.Configuration;
using Umat.Alumni.Paystack.Sdk.Models;
using MemberEntity = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using Umat.Alumni.Paystack.Sdk.Services;
using Umat.Alumni.Member.Api.Models;
using Umat.Alumni.Member.Api.Options;
using Umat.Alumni.Member.Api.Services.Implementations;
using MemberContributionService = Umat.Alumni.Member.Api.Services.Implementations.ContributionService;
using INotificationActor = Umat.Alumni.Member.Api.Services.Interfaces.INotificationActor;
using IAdminNotificationActor = Umat.Alumni.Admin.Api.Services.Interfaces.INotificationActor;
using DbMember = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using Xunit;

namespace Umat.Alumni.Admin.Api.Tests;

public class ContributionMemberForumServiceTests
{
    [Fact]
    public async Task GetContributionsAsync_ReturnsOnlyOwnerContributions_ForNormalAdmin()
    {
        var contributions = new List<Contribution>
        {
            new Contribution { Id = "c1", CreatedBy = "admin1", CampaignId = "camp1", MemberId = "m1", Status = "Pending", TransactionRef = "t1" },
            new Contribution { Id = "c2", CreatedBy = "admin2", CampaignId = "camp1", MemberId = "m2", Status = "Confirmed", TransactionRef = "t2" }
        };

        var mockContributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        mockContributionRepo
            .Setup(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Expression<Func<Contribution, bool>>>()))
            .ReturnsAsync((int page, int pageSize, string sortColumn, string sortDir, Expression<Func<Contribution, bool>> predicate) =>
            {
                var query = contributions.AsQueryable().Where(predicate);
                var results = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                return new PgPagedResult<Contribution>
                {
                    PageIndex = page,
                    PageSize = pageSize,
                    Count = results.Count,
                    TotalCount = query.Count(),
                    TotalPages = 1,
                    LowerBoundSize = 1,
                    UpperBoundSize = results.Count,
                    Results = results
                };
            });

        var service = new AdminContributionService(mockContributionRepo.Object, Mock.Of<IAlumniPgRepository<Campaign>>(), Mock.Of<IAlumniPgRepository<DbMember>>(), Mock.Of<IAdminNotificationActor>(), new NullLogger<AdminContributionService>());
        var admin = new AuthData { Id = "admin1", Role = "Admin" };

        var response = await service.GetContributionsAsync(new ContributionAdminFilter { Page = 1, PageSize = 10 }, admin);

        Assert.Equal(200, response.Code);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data!.Results);
        Assert.Equal("c1", response.Data.Results.Single().Id);
    }

    [Fact]
    public async Task ContributionService_RecordManualContributionAsync_AllowsExternalMember_WhenMemberNotFound()
    {
        Contribution? savedContribution = null;

        var mockContributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        mockContributionRepo.Setup(r => r.AddAsync(It.IsAny<Contribution>()))
            .ReturnsAsync(1)
            .Callback<Contribution>(c => savedContribution = c);

        var mockCampaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        mockCampaignRepo.Setup(r => r.GetByIdAsync("camp1")).ReturnsAsync(new Campaign { Id = "camp1", Title = "Test Campaign" });

        var mockMemberRepo = new Mock<IAlumniPgRepository<DbMember>>();
        mockMemberRepo.Setup(r => r.GetOneAsync(It.IsAny<System.Linq.Expressions.Expression<Func<DbMember, bool>>>())).ReturnsAsync((DbMember?)null);
        mockMemberRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((DbMember?)null);

        var service = new AdminContributionService(mockContributionRepo.Object, mockCampaignRepo.Object, mockMemberRepo.Object, Mock.Of<IAdminNotificationActor>(), new NullLogger<AdminContributionService>());

        var campaign = new Campaign { Id = "camp1", Title = "Test Campaign", CollectedAmount = 0m, PaidCount = 0 };
        mockCampaignRepo.Setup(r => r.GetByIdAsync("camp1")).ReturnsAsync(campaign);

        var result = await service.RecordManualContributionAsync(new RecordManualContributionRequest(
            "camp1",
            "UMaT/CS/2025/9999",
            "Non Platform User",
            "nonplatform@example.com",
            500m,
            "Manual",
            "tx123",
            "Offline donation",
            DateTime.UtcNow,
            true),
            new AuthData { Id = "admin1", Role = "Admin" });

        Assert.Equal(201, result.Code);
        Assert.NotNull(result.Data);
        Assert.Equal("UMaT/CS/2025/9999", result.Data?.MemberId);
        Assert.Equal("Non Platform User", result.Data?.MemberName);
        Assert.Equal("nonplatform@example.com", result.Data?.MemberEmail);
        Assert.Equal("Confirmed", result.Data?.Status);
        Assert.Equal("tx123", result.Data?.TransactionRef);
        Assert.Equal("Offline donation", result.Data?.Notes);
        Assert.Equal(500m, campaign.CollectedAmount);
        Assert.Equal(1, campaign.PaidCount);
        Assert.Equal(savedContribution?.Id, result.Data?.Id);
        Assert.NotNull(savedContribution);
    }

    [Fact]
    public async Task ContributionService_RecordManualContributionAsync_UsesExistingMember_WhenFound()
    {
        Contribution? savedContribution = null;

        var mockContributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        mockContributionRepo.Setup(r => r.AddAsync(It.IsAny<Contribution>()))
            .ReturnsAsync(1)
            .Callback<Contribution>(c => savedContribution = c);

        var existingMember = new DbMember
        {
            Id = "m123",
            FirstName = "Applied",
            LastName = "Member",
            Email = "member@platform.com",
            MemberNumber = "UMaT/CS/2025/1234",
            ProfilePictureUrl = "https://example.com/p.png"
        };

        var mockCampaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        var campaign = new Campaign { Id = "camp1", Title = "Test Campaign", CollectedAmount = 0m, PaidCount = 0 };
        mockCampaignRepo.Setup(r => r.GetByIdAsync("camp1")).ReturnsAsync(campaign);

        var mockMemberRepo = new Mock<IAlumniPgRepository<DbMember>>();
        mockMemberRepo.Setup(r => r.GetOneAsync(It.IsAny<System.Linq.Expressions.Expression<Func<DbMember, bool>>>())).ReturnsAsync(existingMember);
        mockMemberRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync(existingMember);

        var service = new AdminContributionService(mockContributionRepo.Object, mockCampaignRepo.Object, mockMemberRepo.Object, Mock.Of<IAdminNotificationActor>(), new NullLogger<AdminContributionService>());

        var result = await service.RecordManualContributionAsync(new RecordManualContributionRequest(
            "camp1",
            "UMaT/CS/2025/1234",
            null,
            null,
            250m,
            "Manual",
            "tx456",
            "Platform member donation",
            DateTime.UtcNow,
            true),
            new AuthData { Id = "admin1", Role = "Admin" });

        Assert.Equal(201, result.Code);
        Assert.NotNull(result.Data);
        Assert.Equal("m123", result.Data?.MemberId);
        Assert.Equal("Applied Member", result.Data?.MemberName);
        Assert.Equal("member@platform.com", result.Data?.MemberEmail);
        Assert.Equal("Confirmed", result.Data?.Status);
        Assert.Equal("tx456", result.Data?.TransactionRef);
        Assert.Equal("Platform member donation", result.Data?.Notes);
        Assert.Equal(250m, campaign.CollectedAmount);
        Assert.Equal(1, campaign.PaidCount);
        Assert.Equal(savedContribution?.Id, result.Data?.Id);
        Assert.NotNull(savedContribution);
        Assert.Equal("m123", savedContribution?.MemberId);
        Assert.NotNull(savedContribution?.Member);
        Assert.Equal("Applied", savedContribution?.Member?.FirstName);
        Assert.Equal("Member", savedContribution?.Member?.LastName);
        Assert.Equal("member@platform.com", savedContribution?.Member?.Email);
    }

    [Fact]
    public async Task ContributionService_RecordManualContributionAsync_ReturnsBadRequest_WhenMemberIdentifierMissingAndNotFound()
    {
        var mockContributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        var mockCampaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        var mockMemberRepo = new Mock<IAlumniPgRepository<DbMember>>();
        var mockCampaign = new Campaign { Id = "camp1", Title = "Test Campaign" };

        mockCampaignRepo.Setup(r => r.GetByIdAsync("camp1")).ReturnsAsync(mockCampaign);
        mockMemberRepo.Setup(r => r.GetOneAsync(It.IsAny<System.Linq.Expressions.Expression<Func<DbMember, bool>>>())).ReturnsAsync((DbMember?)null);
        mockMemberRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((DbMember?)null);

        var service = new AdminContributionService(mockContributionRepo.Object, mockCampaignRepo.Object, mockMemberRepo.Object, Mock.Of<IAdminNotificationActor>(), new NullLogger<AdminContributionService>());

        var result = await service.RecordManualContributionAsync(new RecordManualContributionRequest(
            "camp1",
            "",
            "Anonymous",
            "anon@example.com",
            100m,
            "Manual",
            "tx-anon",
            "anonymous fallback",
            DateTime.UtcNow,
            true),
            new AuthData { Id = "admin1", Role = "Admin" });

        Assert.Equal(400, result.Code);
        Assert.Equal("MemberNumber is required for manual contributions.", result.Message);
    }

    [Fact]
    public async Task GetMembersAsync_ReturnsOnlyYearGroupMembers_ForNormalAdmin()
    {
        var members = new List<DbMember>
        {
            new DbMember { Id = "m1", GraduationYear = 2026, FirstName = "A" , LastName = "B", Email = "a@x.com", Status = "Active", DepartmentId = "d1", CreatedAt = DateTime.UtcNow },
            new DbMember { Id = "m2", GraduationYear = 2025, FirstName = "C", LastName = "D", Email = "c@x.com", Status = "Active", DepartmentId = "d1", CreatedAt = DateTime.UtcNow }
        };

        var mockMemberRepo = new Mock<IAlumniPgRepository<DbMember>>();
        mockMemberRepo
            .Setup(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Expression<Func<DbMember, bool>>>() ) )
            .ReturnsAsync((int page, int pageSize, string sortColumn, string sortDir, Expression<Func<DbMember, bool>> predicate) =>
            {
                var query = members.AsQueryable().Where(predicate);
                var results = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                return new PgPagedResult<DbMember>
                {
                    PageIndex = page,
                    PageSize = pageSize,
                    Count = results.Count,
                    TotalCount = query.Count(),
                    TotalPages = 1,
                    LowerBoundSize = 1,
                    UpperBoundSize = results.Count,
                    Results = results
                };
            });

        var mockCampaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        var mockContributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        var service = new MemberManagementService(mockMemberRepo.Object, mockCampaignRepo.Object, mockContributionRepo.Object, new NullLogger<MemberManagementService>());
        var admin = new AuthData { Id = "admin1", Role = "Admin", GraduationYear = 2026 };

        var listResponse = await service.GetMembersAsync(new MemberListFilter { Page = 1, PageSize = 10 }, admin);
        Assert.Equal(200, listResponse.Code);
        Assert.Single(listResponse.Data!.Results);
        Assert.Equal("m1", listResponse.Data.Results.Single().Id);

        mockMemberRepo.Setup(r => r.GetByIdAsync("m2")).ReturnsAsync(members[1]);
        var notFoundResponse = await service.GetMemberByIdAsync("m2", admin);
        Assert.Equal(404, notFoundResponse.Code);
    }

    [Fact]
    public async Task CreateCategoryAsync_AndThreadActions_ReturnForbidden_ForNonSuperAdmin()
    {
        var mockCategoryRepo = new Mock<IAlumniPgRepository<ForumCategory>>();
        var mockThreadRepo = new Mock<IAlumniPgRepository<ForumThread>>();

        var service = new ForumService(mockCategoryRepo.Object, mockThreadRepo.Object, new NullLogger<ForumService>());
        var admin = new AuthData { Id = "admin1", Role = "Admin" };

        var catResponse = await service.CreateCategoryAsync("Test", "Desc", admin);
        Assert.Equal(403, catResponse.Code);

        var thread = new ForumThread { Id = "t1", IsPinned = false, IsClosed = false };
        mockThreadRepo.Setup(r => r.GetByIdAsync("t1")).ReturnsAsync(thread);

        var pinResponse = await service.PinThreadAsync("t1", admin);
        Assert.Equal(403, pinResponse.Code);

        var closeResponse = await service.CloseThreadAsync("t1", admin);
        Assert.Equal(403, closeResponse.Code);

        var deleteResponse = await service.DeleteThreadAsync("t1", admin);
        Assert.Equal(403, deleteResponse.Code);
    }

    [Fact]
    public async Task MentorshipService_Actions_ReturnForbidden_ForNonSuperAdmin()
    {
        var mockProfileRepo = new Mock<IAlumniPgRepository<MentorProfile>>();
        var mockRequestRepo = new Mock<IAlumniPgRepository<MentorshipRequest>>();

        var service = new MentorshipService(mockProfileRepo.Object, mockRequestRepo.Object, new NullLogger<MentorshipService>());
        var admin = new AuthData { Id = "admin1", Role = "Admin" };

        var profilesResponse = await service.GetMentorProfilesAsync(new MentorProfileFilter { Page = 1, PageSize = 10 }, admin);
        Assert.Equal(403, profilesResponse.Code);

        var requestsResponse = await service.GetRequestsAsync(new MentorshipRequestFilter { Page = 1, PageSize = 10 }, admin);
        Assert.Equal(403, requestsResponse.Code);

        var stubProfile = new MentorProfile { Id = "p1", Status = "Pending" };
        mockProfileRepo.Setup(r => r.GetByIdAsync("p1")).ReturnsAsync(stubProfile);

        var approveResponse = await service.ApproveMentorAsync("p1", admin);
        Assert.Equal(403, approveResponse.Code);

        var rejectResponse = await service.RejectMentorAsync("p1", admin);
        Assert.Equal(403, rejectResponse.Code);
    }

    [Fact]
    public async Task ForumService_SuperAdmin_CanCreatePinCloseDeleteThreadActions()
    {
        var mockCategoryRepo = new Mock<IAlumniPgRepository<ForumCategory>>();
        var mockThreadRepo = new Mock<IAlumniPgRepository<ForumThread>>();

        ForumCategory createdCategory = null!;
        mockCategoryRepo.Setup(r => r.AddAsync(It.IsAny<ForumCategory>()))
            .ReturnsAsync(1)
            .Callback<ForumCategory>(c => createdCategory = c);

        var thread = new ForumThread { Id = "t1", IsPinned = false, IsClosed = false };
        mockThreadRepo.Setup(r => r.GetByIdAsync("t1")).ReturnsAsync(thread);
        mockThreadRepo.Setup(r => r.UpdateAsync(It.IsAny<ForumThread>())).ReturnsAsync(1);
        mockThreadRepo.Setup(r => r.RemoveAsync(It.IsAny<ForumThread>())).ReturnsAsync(1);

        var service = new ForumService(mockCategoryRepo.Object, mockThreadRepo.Object, new NullLogger<ForumService>());
        var superAdmin = new AuthData { Id = "super1", Role = "SuperAdmin" };

        var createResult = await service.CreateCategoryAsync("TestCat", "Desc", superAdmin);
        Assert.Equal(201, createResult.Code);
        Assert.NotNull(createdCategory);
        Assert.Equal("TestCat", createdCategory.Name);

        var pinResult = await service.PinThreadAsync("t1", superAdmin);
        Assert.Equal(200, pinResult.Code);
        Assert.True(thread.IsPinned);

        var closeResult = await service.CloseThreadAsync("t1", superAdmin);
        Assert.Equal(200, closeResult.Code);
        Assert.True(thread.IsClosed);

        var deleteResult = await service.DeleteThreadAsync("t1", superAdmin);
        Assert.Equal(200, deleteResult.Code);
        mockThreadRepo.Verify(r => r.RemoveAsync(It.Is<ForumThread>(f => f.Id == "t1")), Times.Once);
    }

    [Fact]
    public async Task MentorshipService_SuperAdmin_CanGetProfilesRequestsAndApproveReject()
    {
        var mockProfileRepo = new Mock<IAlumniPgRepository<MentorProfile>>();
        var mockRequestRepo = new Mock<IAlumniPgRepository<MentorshipRequest>>();

        var mentorProfiles = new List<MentorProfile> { new MentorProfile { Id = "p1", Status = "Pending" } };
        mockProfileRepo.Setup(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Expression<Func<MentorProfile, bool>>>() ))
            .ReturnsAsync(new PgPagedResult<MentorProfile> { PageIndex = 1, PageSize = 10, Count = 1, TotalCount = 1, TotalPages = 1, Results = mentorProfiles });

        var mentorshipRequests = new List<MentorshipRequest> { new MentorshipRequest { Id = "r1", Status = "Pending" } };
        mockRequestRepo.Setup(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Expression<Func<MentorshipRequest, bool>>>() ))
            .ReturnsAsync(new PgPagedResult<MentorshipRequest> { PageIndex = 1, PageSize = 10, Count = 1, TotalCount = 1, TotalPages = 1, Results = mentorshipRequests });

        mockProfileRepo.Setup(r => r.GetByIdAsync("p1")).ReturnsAsync(mentorProfiles[0]);
        mockProfileRepo.Setup(r => r.UpdateAsync(It.IsAny<MentorProfile>())).ReturnsAsync(1);

        var service = new MentorshipService(mockProfileRepo.Object, mockRequestRepo.Object, new NullLogger<MentorshipService>());
        var superAdmin = new AuthData { Id = "super1", Role = "SuperAdmin" };

        var profileResponse = await service.GetMentorProfilesAsync(new MentorProfileFilter { Page = 1, PageSize = 10 }, superAdmin);
        Assert.Equal(200, profileResponse.Code);
        Assert.NotNull(profileResponse.Data);
        Assert.Single(profileResponse.Data.Results);

        var requestResponse = await service.GetRequestsAsync(new MentorshipRequestFilter { Page = 1, PageSize = 10 }, superAdmin);
        Assert.Equal(200, requestResponse.Code);
        Assert.NotNull(requestResponse.Data);
        Assert.Single(requestResponse.Data.Results);

        var approveResponse = await service.ApproveMentorAsync("p1", superAdmin);
        Assert.Equal(200, approveResponse.Code);

        mentorProfiles[0].Status = "Pending";
        var rejectResponse = await service.RejectMentorAsync("p1", superAdmin);
        Assert.Equal(200, rejectResponse.Code);
    }

    [Fact]
    public async Task MemberMentorshipService_RegisterAsMentorAsync_ResubmitsRejectedProfile()
    {
        var existingProfile = new MentorProfile
        {
            Id = "p1",
            MemberId = "m1",
            Area = "Old area",
            Bio = "Old bio",
            MaxMentees = 2,
            Status = "Rejected",
            Member = new MemberSnapshot { Id = "m1", FirstName = "John", LastName = "Doe", Email = "john@example.com" }
        };

        var mockProfileRepo = new Mock<IAlumniPgRepository<MentorProfile>>();
        mockProfileRepo
            .Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<MentorProfile, bool>>>() ))
            .ReturnsAsync(existingProfile);
        mockProfileRepo
            .Setup(r => r.UpdateAsync(It.IsAny<MentorProfile>()))
            .ReturnsAsync(1);

        var service = new MemberMentorshipService(mockProfileRepo.Object, Mock.Of<IAlumniPgRepository<MentorshipRequest>>(), new NullLogger<MemberMentorshipService>());
        var member = new AuthData { Id = "m1", Role = "Member" };

        var response = await service.RegisterAsMentorAsync(new RegisterAsMentorRequest("New Area", "New bio", 5), member);

        Assert.Equal(201, response.Code);
        mockProfileRepo.Verify(r => r.UpdateAsync(It.Is<MentorProfile>(p => p.Id == "p1" && p.Status == "Pending" && p.Area == "New Area" && p.Bio == "New bio" && p.MaxMentees == 5)), Times.Once);
    }

    [Fact]
    public async Task MemberMentorshipService_RegisterAsMentorAsync_RejectsPendingProfile()
    {
        var existingProfile = new MentorProfile
        {
            Id = "p1",
            MemberId = "m1",
            Status = "Pending"
        };

        var mockProfileRepo = new Mock<IAlumniPgRepository<MentorProfile>>();
        mockProfileRepo
            .Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<MentorProfile, bool>>>() ))
            .ReturnsAsync(existingProfile);

        var service = new MemberMentorshipService(mockProfileRepo.Object, Mock.Of<IAlumniPgRepository<MentorshipRequest>>(), new NullLogger<MemberMentorshipService>());
        var member = new AuthData { Id = "m1", Role = "Member" };

        var response = await service.RegisterAsMentorAsync(new RegisterAsMentorRequest("Any", null, 3), member);

        Assert.Equal(409, response.Code);
        Assert.Equal("You already have a pending mentor profile", response.Message);
        mockProfileRepo.Verify(r => r.UpdateAsync(It.IsAny<MentorProfile>()), Times.Never);
    }

    [Fact]
    public async Task MemberMentorshipService_RegisterAsMentorAsync_RejectsApprovedProfile()
    {
        var existingProfile = new MentorProfile
        {
            Id = "p1",
            MemberId = "m1",
            Status = "Approved"
        };

        var mockProfileRepo = new Mock<IAlumniPgRepository<MentorProfile>>();
        mockProfileRepo
            .Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<MentorProfile, bool>>>() ))
            .ReturnsAsync(existingProfile);

        var service = new MemberMentorshipService(mockProfileRepo.Object, Mock.Of<IAlumniPgRepository<MentorshipRequest>>(), new NullLogger<MemberMentorshipService>());
        var member = new AuthData { Id = "m1", Role = "Member" };

        var response = await service.RegisterAsMentorAsync(new RegisterAsMentorRequest("Any", null, 3), member);

        Assert.Equal(409, response.Code);
        Assert.Equal("You already have an approved mentor profile", response.Message);
        mockProfileRepo.Verify(r => r.UpdateAsync(It.IsAny<MentorProfile>()), Times.Never);
    }

    [Fact]
    public async Task MemberMentorshipService_RequestMentorshipAsync_RejectsSelfMentorship()
    {
        var mentorProfile = new MentorProfile
        {
            Id = "p1",
            MemberId = "m1",
            Status = "Approved"
        };

        var mockProfileRepo = new Mock<IAlumniPgRepository<MentorProfile>>();
        mockProfileRepo.Setup(r => r.GetByIdAsync("p1")).ReturnsAsync(mentorProfile);

        var service = new MemberMentorshipService(mockProfileRepo.Object, Mock.Of<IAlumniPgRepository<MentorshipRequest>>(), new NullLogger<MemberMentorshipService>());
        var member = new AuthData { Id = "m1", Role = "Member" };

        var response = await service.RequestMentorshipAsync(new RequestMentorshipRequest("p1", "Data Science", "Please mentor me"), member);

        Assert.Equal(400, response.Code);
        Assert.Equal("You cannot request mentorship from your own profile", response.Message);
    }

    [Fact]
    public void ResolveYearGroupsForCreation_EnforcesAdminGraduationYear_ForNonSuperAdmin()
    {
        var admin = new AuthData { Role = "Admin", GraduationYear = 2026 };
        var resolved = admin.ResolveYearGroupsForCreation(new List<int> { 2024, 2025 });

        Assert.NotNull(resolved);
        Assert.Single(resolved);
        Assert.Equal(2026, resolved![0]);
    }

    [Fact]
    public async Task EventService_CreateEventAsync_RespectsAdminYearGroup_ForNormalAdmin()
    {
        var mockEventRepo = new Mock<IAlumniPgRepository<AlumniEvent>>();
        var mockRsvpRepo = new Mock<IAlumniPgRepository<EventRsvp>>();
        var mockMemberRepo = new Mock<IAlumniPgRepository<Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member>>();
        var mockStorage = new Mock<IStorageService>();

        AlumniEvent createdEvent = null!;
        mockEventRepo
            .Setup(r => r.AddAsync(It.IsAny<AlumniEvent>()))
            .ReturnsAsync(1)
            .Callback<AlumniEvent>(e => createdEvent = e);

        var service = new EventService(mockEventRepo.Object, mockRsvpRepo.Object, mockMemberRepo.Object, mockStorage.Object, Mock.Of<IAdminNotificationActor>(), new NullLogger<EventService>());
        var admin = new AuthData { Id = "admin1", Role = "Admin", GraduationYear = 2026 };

        var request = new CreateEventRequest
        {
            Title = "Test Event",
            Description = "Desc",
            Venue = "Venue",
            StartDate = DateTime.UtcNow.AddDays(1),
            YearGroups = new List<int> { 2024, 2025 }
        };

        var response = await service.CreateEventAsync(request, admin);
        Assert.Equal(201, response.Code);
        Assert.NotNull(createdEvent);
        Assert.NotNull(createdEvent.YearGroups);
        Assert.Single(createdEvent.YearGroups);
        Assert.Equal(2026, createdEvent.YearGroups![0]);
    }

    [Fact]
    public async Task EventService_CreateEventAsync_RejectsPaidEvent()
    {
        var mockEventRepo = new Mock<IAlumniPgRepository<AlumniEvent>>();
        var mockRsvpRepo = new Mock<IAlumniPgRepository<EventRsvp>>();
        var mockMemberRepo = new Mock<IAlumniPgRepository<Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member>>();
        var mockStorage = new Mock<IStorageService>();

        var service = new EventService(mockEventRepo.Object, mockRsvpRepo.Object, mockMemberRepo.Object, mockStorage.Object, Mock.Of<IAdminNotificationActor>(), new NullLogger<EventService>());
        var admin = new AuthData { Id = "admin1", Role = "Admin", GraduationYear = 2026 };

        var request = new CreateEventRequest
        {
            Title = "Paid Event",
            Description = "Should fail",
            Venue = "Venue",
            StartDate = DateTime.UtcNow.AddDays(1),
            IsTicketed = true,
            TicketPrice = 100m,
            YearGroups = new List<int> { 2026 }
        };

        var response = await service.CreateEventAsync(request, admin);
        Assert.Equal(400, response.Code);
        Assert.Equal("Paid events are not supported yet. Please create a free event.", response.Message);
    }

    [Fact]
    public async Task CampaignService_CreateCampaignAsync_RespectsAdminYearGroup_ForNormalAdmin()
    {
        var mockCampaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        var mockStorage = new Mock<IStorageService>();

        Campaign createdCampaign = null!;
        mockCampaignRepo
            .Setup(r => r.AddAsync(It.IsAny<Campaign>()))
            .ReturnsAsync(1)
            .Callback<Campaign>(c => createdCampaign = c);

        var mockContributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        var mockMemberRepo = new Mock<IAlumniPgRepository<MemberEntity>>();
        var service = new AdminCampaignService(mockCampaignRepo.Object, mockContributionRepo.Object, mockMemberRepo.Object, mockStorage.Object, Mock.Of<IAdminNotificationActor>(), new NullLogger<AdminCampaignService>());
        var admin = new AuthData { Id = "admin1", Role = "Admin", GraduationYear = 2026 };

        var request = new CreateCampaignRequest
        {
            Title = "Test Campaign",
            Description = "Desc",
            Deadline = DateTime.UtcNow.AddDays(30),
            TargetAmount = 1000,
            AmountPerMember = 100,
            YearGroups = new List<int> { 2024, 2025 }
        };

        var response = await service.CreateCampaignAsync(request, admin);
        Assert.Equal(201, response.Code);
        Assert.NotNull(createdCampaign);
        Assert.NotNull(createdCampaign.YearGroups);
        Assert.Single(createdCampaign.YearGroups);
        Assert.Equal(2026, createdCampaign.YearGroups![0]);
    }

    [Fact]
    public async Task CampaignService_CreateMembershipCampaignWithYear_SuperAdminSuccess()
    {
        var mockCampaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        var mockStorage = new Mock<IStorageService>();

        Campaign createdCampaign = null!;
        mockCampaignRepo
            .Setup(r => r.AddAsync(It.IsAny<Campaign>()))
            .ReturnsAsync(1)
            .Callback<Campaign>(c => createdCampaign = c);

        var mockContributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        var mockMemberRepo = new Mock<IAlumniPgRepository<MemberEntity>>();
        var service = new AdminCampaignService(mockCampaignRepo.Object, mockContributionRepo.Object, mockMemberRepo.Object, mockStorage.Object, Mock.Of<IAdminNotificationActor>(), new NullLogger<AdminCampaignService>());
        var admin = new AuthData { Id = "superadmin", Role = "SuperAdmin", GraduationYear = 2025 };

        var request = new CreateCampaignRequest
        {
            Title = "Membership 2025",
            Description = "Membership plan 2025",
            Deadline = DateTime.UtcNow.AddDays(30),
            TargetAmount = 10000,
            AmountPerMember = 100,
            IsMembershipCampaign = true,
            MembershipYear = DateTime.UtcNow.Year,
            YearGroups = new List<int> { 2025 }
        };

        var response = await service.CreateCampaignAsync(request, admin);
        Assert.Equal(201, response.Code);
        Assert.NotNull(createdCampaign);
        Assert.True(createdCampaign.IsMembershipCampaign);
        Assert.Equal(DateTime.UtcNow.Year, createdCampaign.MembershipYear);
    }

    [Fact]
    public async Task CampaignService_CreateCampaignAsync_RejectsMembershipCampaign_ByNormalAdmin()
    {
        var mockCampaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        var mockStorage = new Mock<IStorageService>();
        var mockContributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        var mockMemberRepo = new Mock<IAlumniPgRepository<MemberEntity>>();

        var service = new AdminCampaignService(mockCampaignRepo.Object, mockContributionRepo.Object, mockMemberRepo.Object, mockStorage.Object, Mock.Of<IAdminNotificationActor>(), new NullLogger<AdminCampaignService>());
        var admin = new AuthData { Id = "admin1", Role = "Admin", GraduationYear = 2026 };

        var request = new CreateCampaignRequest
        {
            Title = "Membership Campaign",
            Description = "Test",
            Deadline = DateTime.UtcNow.AddDays(30),
            TargetAmount = 1000,
            AmountPerMember = 100,
            IsMembershipCampaign = true
        };

        var response = await service.CreateCampaignAsync(request, admin);
        Assert.Equal(403, response.Code);
        Assert.Equal("Only super admins can create membership campaigns.", response.Message);
    }

    [Fact]
    public async Task ContributionService_InitiatePaystackPayment_ReturnsNotFound_WhenCampaignMissing()
    {
        var mockContributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        var mockCampaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        var mockMemberRepo = new Mock<IAlumniPgRepository<Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member>>();
        var mockPaymentTransactionRepo = new Mock<IAlumniPgRepository<PaymentTransaction>>();
        var mockPaystackService = new Mock<IPaystackService>();
        var mockRedis = new Mock<IRedisService<MemberRedisConfig>>();
        var config = new Mock<IConfiguration>();
        var logger = new NullLogger<MemberContributionService>();

        mockCampaignRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((Campaign?)null);

        var service = new MemberContributionService(
            mockContributionRepo.Object,
            mockCampaignRepo.Object,
            mockMemberRepo.Object,
            mockPaymentTransactionRepo.Object,
            mockPaystackService.Object,
            mockRedis.Object,
            Mock.Of<INotificationActor>(),
            config.Object,
            logger);

        var output = await service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("x", 100m), new AuthData { Id = "m1", Email = "test@example.com" });

        Assert.Equal(404, output.Code);
        Assert.Equal("Campaign not found", output.Message);
    }

    [Fact]
    public async Task ContributionService_InitiateMembershipRenewalAsync_CreatesManualContribution_WhenAllowed()
    {
        var mockContributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        var mockCampaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        var mockMemberRepo = new Mock<IAlumniPgRepository<Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member>>();
        var mockPaymentTransactionRepo = new Mock<IAlumniPgRepository<PaymentTransaction>>();
        var mockPaystackService = new Mock<IPaystackService>();
        var mockRedis = new Mock<IRedisService<MemberRedisConfig>>();
        var config = new Mock<IConfiguration>();
        var logger = new NullLogger<MemberContributionService>();

        mockCampaignRepo.Setup(r => r.GetByIdAsync("membership-campaign")).ReturnsAsync(new Campaign { Id = "membership-campaign", Title = "Membership", AmountPerMember = 100, IsMembershipCampaign = true, AllowManualPayments = true });

        Contribution savedContribution = null!;
        mockContributionRepo.Setup(r => r.AddAsync(It.IsAny<Contribution>())).ReturnsAsync(1).Callback<Contribution>(c => savedContribution = c);

        var service = new MemberContributionService(
            mockContributionRepo.Object,
            mockCampaignRepo.Object,
            mockMemberRepo.Object,
            mockPaymentTransactionRepo.Object,
            mockPaystackService.Object,
            mockRedis.Object,
            Mock.Of<INotificationActor>(),
            config.Object,
            logger);

        var response = await service.InitiateMembershipRenewalAsync(new InitiateMembershipRenewalRequest("membership-campaign", 1, "manual"), new AuthData { Id = "m1", Email = "john@example.com" });

        Assert.Equal(200, response.Code);
        Assert.NotNull(response.Data);
        Assert.Equal(1, savedContribution.Amount / 100);
        Assert.Equal("Pending", savedContribution.Status);
        Assert.Equal("Membership renewal for 1 year(s)", savedContribution.Notes);
    }

    [Fact]
    public async Task ContributionService_InitiateMembershipRenewalAsync_RejectsWhenAlreadyPaid()
    {
        var mockContributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        var mockCampaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        var mockMemberRepo = new Mock<IAlumniPgRepository<Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member>>();
        var mockPaymentTransactionRepo = new Mock<IAlumniPgRepository<PaymentTransaction>>();
        var mockPaystackService = new Mock<IPaystackService>();
        var mockRedis = new Mock<IRedisService<MemberRedisConfig>>();
        var config = new Mock<IConfiguration>();
        var logger = new NullLogger<MemberContributionService>();

        mockCampaignRepo.Setup(r => r.GetByIdAsync("membership-campaign")).ReturnsAsync(new Campaign { Id = "membership-campaign", Title = "Membership", AmountPerMember = 100, IsMembershipCampaign = true, AllowManualPayments = true });
        mockContributionRepo.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<Contribution, bool>>>())).ReturnsAsync(new Contribution { Id = "c1", MemberId = "m1", CampaignId = "membership-campaign", Status = "Confirmed" });

        var service = new MemberContributionService(mockContributionRepo.Object, mockCampaignRepo.Object, mockMemberRepo.Object, mockPaymentTransactionRepo.Object, mockPaystackService.Object, mockRedis.Object, Mock.Of<INotificationActor>(), config.Object, logger);

        var response = await service.InitiateMembershipRenewalAsync(new InitiateMembershipRenewalRequest("membership-campaign", 1, "manual"), new AuthData { Id = "m1", Email = "john@example.com" });

        Assert.Equal(400, response.Code);
        Assert.Equal("You have already paid this membership campaign.", response.Message);
    }

    [Fact]
    public async Task ContributionService_GetMembershipStatusAsync_ReturnsCurrentValue()
    {
        var mockContributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        var mockCampaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        var mockMemberRepo = new Mock<IAlumniPgRepository<Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member>>();
        var mockPaymentTransactionRepo = new Mock<IAlumniPgRepository<PaymentTransaction>>();
        var mockPaystackService = new Mock<IPaystackService>();
        var mockRedis = new Mock<IRedisService<MemberRedisConfig>>();
        var config = new Mock<IConfiguration>();
        var logger = new NullLogger<MemberContributionService>();

        mockMemberRepo.Setup(r => r.GetByIdAsync("m1")).ReturnsAsync(new Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member
        {
            Id = "m1",
            IsMembershipActive = true,
            MembershipExpiry = DateTime.UtcNow.AddYears(1),
            MembershipYearsPaid = 1,
            LastMembershipPaidAt = DateTime.UtcNow
        });

        var service = new MemberContributionService(
            mockContributionRepo.Object,
            mockCampaignRepo.Object,
            mockMemberRepo.Object,
            mockPaymentTransactionRepo.Object,
            mockPaystackService.Object,
            mockRedis.Object,
            Mock.Of<INotificationActor>(),
            config.Object,
            logger);

        var response = await service.GetMembershipStatusAsync(new AuthData { Id = "m1" });

        Assert.Equal(200, response.Code);
        Assert.NotNull(response.Data);
        Assert.True(response.Data!.IsMembershipActive);
        Assert.Equal(1, response.Data.MembershipYearsPaid);
    }

    [Fact]
    public async Task ContributionService_GetMembershipStatusAsync_ReturnsActive_WhenCurrentMembershipCampaignPaid()
    {
        var mockContributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        var mockCampaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        var mockMemberRepo = new Mock<IAlumniPgRepository<Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member>>();
        var mockPaymentTransactionRepo = new Mock<IAlumniPgRepository<PaymentTransaction>>();
        var mockPaystackService = new Mock<IPaystackService>();
        var mockRedis = new Mock<IRedisService<MemberRedisConfig>>();
        var config = new Mock<IConfiguration>();
        var logger = new NullLogger<MemberContributionService>();

        var currentYear = DateTime.UtcNow.Year;
        var campaign = new Campaign { Id = "c1", Title = "Membership", IsMembershipCampaign = true, MembershipYear = currentYear, Status = CampaignStatus.Active };

        mockMemberRepo.Setup(r => r.GetByIdAsync("m1")).ReturnsAsync(new Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member { Id = "m1" });
        mockCampaignRepo.Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<Campaign, bool>>>())).ReturnsAsync(new List<Campaign> { campaign });
        mockContributionRepo.Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<Contribution, bool>>>())).ReturnsAsync(new List<Contribution> { new Contribution { Id = "pay1", MemberId = "m1", CampaignId = "c1", Status = "Confirmed" } });

        var service = new MemberContributionService(
            mockContributionRepo.Object,
            mockCampaignRepo.Object,
            mockMemberRepo.Object,
            mockPaymentTransactionRepo.Object,
            mockPaystackService.Object,
            mockRedis.Object,
            Mock.Of<INotificationActor>(),
            config.Object,
            logger);

        var response = await service.GetMembershipStatusAsync(new AuthData { Id = "m1" });

        Assert.Equal(200, response.Code);
        Assert.NotNull(response.Data);
        Assert.True(response.Data!.IsMembershipActive);
        Assert.Equal(new DateTime(currentYear, 12, 31, 23, 59, 59, DateTimeKind.Utc), response.Data.MembershipExpiry);
    }

    [Fact]
    public async Task ContributionService_GetMembershipStatusAsync_ReturnsExpired_WhenOnlyFutureCampaignPaid()
    {
        var mockContributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        var mockCampaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        var mockMemberRepo = new Mock<IAlumniPgRepository<Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member>>();
        var mockPaymentTransactionRepo = new Mock<IAlumniPgRepository<PaymentTransaction>>();
        var mockPaystackService = new Mock<IPaystackService>();
        var mockRedis = new Mock<IRedisService<MemberRedisConfig>>();
        var config = new Mock<IConfiguration>();
        var logger = new NullLogger<MemberContributionService>();

        var currentYear = DateTime.UtcNow.Year;
        var futureYear = currentYear + 1;

        var currentCampaign = new Campaign { Id = "c-current", Title = "Current Membership", IsMembershipCampaign = true, MembershipYear = currentYear, Status = CampaignStatus.Active };
        var futureCampaign = new Campaign { Id = "c-future", Title = "Future Membership", IsMembershipCampaign = true, MembershipYear = futureYear, Status = CampaignStatus.Active };

        mockMemberRepo.Setup(r => r.GetByIdAsync("m1")).ReturnsAsync(new Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member { Id = "m1" });
        mockCampaignRepo.Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<Campaign, bool>>>())).ReturnsAsync(new List<Campaign> { currentCampaign });
        mockContributionRepo.Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<Contribution, bool>>>())).ReturnsAsync(new List<Contribution> { new Contribution { Id = "pay-future", MemberId = "m1", CampaignId = "c-future", Status = "Confirmed" } });

        var service = new MemberContributionService(
            mockContributionRepo.Object,
            mockCampaignRepo.Object,
            mockMemberRepo.Object,
            mockPaymentTransactionRepo.Object,
            mockPaystackService.Object,
            mockRedis.Object,
            Mock.Of<INotificationActor>(),
            config.Object,
            logger);

        var response = await service.GetMembershipStatusAsync(new AuthData { Id = "m1" });

        Assert.Equal(200, response.Code);
        Assert.NotNull(response.Data);
        Assert.False(response.Data!.IsMembershipActive);
    }

    [Fact]
    public async Task ContributionService_UploadProofAsync_CreatesContribution_WhenCampaignExists()
    {
        var mockContributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        var mockCampaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        var mockMemberRepo = new Mock<IAlumniPgRepository<Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member>>();
        var mockPaymentTransactionRepo = new Mock<IAlumniPgRepository<PaymentTransaction>>();
        var mockPaystackService = new Mock<IPaystackService>();
        var mockRedis = new Mock<IRedisService<MemberRedisConfig>>();
        var config = new Mock<IConfiguration>();
        var logger = new NullLogger<MemberContributionService>();

        mockCampaignRepo.Setup(r => r.GetByIdAsync("c1")).ReturnsAsync(new Campaign { Id = "c1", Title = "Camp" });

        Contribution savedContribution = null!;
        mockContributionRepo.Setup(r => r.AddAsync(It.IsAny<Contribution>())).ReturnsAsync(1).Callback<Contribution>(c => savedContribution = c);

        var service = new MemberContributionService(
            mockContributionRepo.Object,
            mockCampaignRepo.Object,
            mockMemberRepo.Object,
            mockPaymentTransactionRepo.Object,
            mockPaystackService.Object,
            mockRedis.Object,
            Mock.Of<INotificationActor>(),
            config.Object,
            logger);

        var result = await service.UploadProofAsync(new UploadContributionProofRequest("c1", "ref1", "proof"), new AuthData { Id = "m1", FirstName = "John", LastName = "Doe", Email = "john@example.com" });

        Assert.Equal(201, result.Code);
        Assert.NotNull(result.Data);
        Assert.Equal("Pending", result.Data!.Status);
        Assert.Equal("c1", result.Data.CampaignId);
        Assert.Equal("m1", result.Data.MemberId);
        Assert.Equal("ref1", result.Data.TransactionRef);
        Assert.Equal("Manual", result.Data.PaymentMethod);
        Assert.Equal("proof", result.Data.Notes);
        Assert.Equal(savedContribution?.Id, result.Data.Id);
    }

    [Fact]
    public async Task ContributionService_GetMyContributionsAsync_FiltersByMemberAndCampaign()
    {
        var contributions = new List<Contribution>
        {
            new Contribution { Id = "c1", MemberId = "m1", CampaignId = "camp1" },
            new Contribution { Id = "c2", MemberId = "m1", CampaignId = "camp2" },
            new Contribution { Id = "c3", MemberId = "m2", CampaignId = "camp1" },
        };

        var mockContributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        mockContributionRepo
            .Setup(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Expression<Func<Contribution, bool>>>() ) )
            .ReturnsAsync((int page, int pageSize, string sortColumn, string sortDir, Expression<Func<Contribution, bool>> predicate) =>
            {
                var query = contributions.AsQueryable().Where(predicate);
                var results = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                return new PgPagedResult<Contribution>
                {
                    PageIndex = page,
                    PageSize = pageSize,
                    Count = results.Count,
                    TotalCount = query.Count(),
                    TotalPages = 1,
                    LowerBoundSize = 1,
                    UpperBoundSize = results.Count,
                    Results = results
                };
            });

        var service = new MemberContributionService(
            mockContributionRepo.Object,
            Mock.Of<IAlumniPgRepository<Campaign>>(),
            Mock.Of<IAlumniPgRepository<DbMember>>(),
            Mock.Of<IAlumniPgRepository<PaymentTransaction>>(),
            Mock.Of<IPaystackService>(),
            Mock.Of<IRedisService<MemberRedisConfig>>(),
            Mock.Of<INotificationActor>(),
            Mock.Of<IConfiguration>(),
            new NullLogger<MemberContributionService>());

        var response = await service.GetMyContributionsAsync("m1", new ContributionFilter { Page = 1, PageSize = 10, CampaignId = "camp1" });

        Assert.Equal(200, response.Code);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data.Results);
        Assert.Equal("c1", response.Data.Results.First().Id);
    }

    [Fact]
    public async Task ContributionService_InitiatePaystackPaymentAsync_Success_ForExistingCampaign()
    {
        var mockContributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        var mockCampaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        var mockMemberRepo = new Mock<IAlumniPgRepository<Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member>>();
        var mockPaymentTransactionRepo = new Mock<IAlumniPgRepository<PaymentTransaction>>();
        var mockPaystackService = new Mock<IPaystackService>();
        var mockRedis = new Mock<IRedisService<MemberRedisConfig>>();
        var config = new Mock<IConfiguration>();
        var logger = new NullLogger<MemberContributionService>();

        mockCampaignRepo.Setup(r => r.GetByIdAsync("camp1")).ReturnsAsync(new Campaign { Id = "camp1", Title = "TestCampaign" });
        mockMemberRepo.Setup(r => r.GetByIdAsync("member1")).ReturnsAsync(new Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Member { Id = "member1", FirstName = "First", LastName = "Last", Email = "email@example.com" });

        mockPaystackService.Setup(p => p.InitializePaymentAsync(It.IsAny<InitializePaymentRequest>())).ReturnsAsync(new InitializePaymentResponse
        {
            Status = true,
            Message = "success",
            Data = new InitializePaymentData
            {
                Reference = "ref-123",
                AuthorizationUrl = "https://paystack.test/auth"
            }
        });

        mockPaymentTransactionRepo.Setup(r => r.AddAsync(It.IsAny<PaymentTransaction>())).ReturnsAsync(1);

        var service = new MemberContributionService(
            mockContributionRepo.Object,
            mockCampaignRepo.Object,
            mockMemberRepo.Object,
            mockPaymentTransactionRepo.Object,
            mockPaystackService.Object,
            mockRedis.Object,
            Mock.Of<INotificationActor>(),
            config.Object,
            logger);

        var result = await service.InitiatePaystackPaymentAsync(new InitiatePaystackPaymentRequest("camp1", 100.00m), new AuthData { Id = "member1", Email = "email@example.com" });

        Assert.Equal(200, result.Code);
        Assert.NotNull(result.Data);
        Assert.Equal("Payment initiated", result.Message);

        mockPaymentTransactionRepo.Verify(r => r.AddAsync(It.IsAny<PaymentTransaction>()), Times.Once);
        // We can't easily assert private nested type, just ensure SetAsync was called once.
        mockRedis.Verify(r => r.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()), Times.Once);
    }
}
