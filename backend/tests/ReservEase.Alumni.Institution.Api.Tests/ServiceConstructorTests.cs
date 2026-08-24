using System;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;
using Xunit;

using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class ServiceConstructorTests
{
    [Fact]
    public void CanConstructAllServiceImplementations()
    {
        var adminRepo = new Mock<IAlumniPgRepository<StaffEntity>>();
        var memberRepo = new Mock<IAlumniPgRepository<MemberEntity>>();
        var campaignRepo = new Mock<IAlumniPgRepository<Campaign>>();
        var contributionRepo = new Mock<IAlumniPgRepository<Contribution>>();
        var eventRepo = new Mock<IAlumniPgRepository<AlumniEvent>>();
        var eventRsvpRepo = new Mock<IAlumniPgRepository<EventRsvp>>();
        var jobRepo = new Mock<IAlumniPgRepository<Job>>();
        var newsRepo = new Mock<IAlumniPgRepository<NewsPost>>();
        var forumCategoryRepo = new Mock<IAlumniPgRepository<ForumCategory>>();
        var forumThreadRepo = new Mock<IAlumniPgRepository<ForumThread>>();
        var forumPostRepo = new Mock<IAlumniPgRepository<ForumPost>>();
        var mentorProfileRepo = new Mock<IAlumniPgRepository<MentorProfile>>();
        var mentorshipRequestRepo = new Mock<IAlumniPgRepository<MentorshipRequest>>();
        var resourceRepo = new Mock<IAlumniPgRepository<Resource>>();
        var institutionRepo = new Mock<IAlumniPgRepository<InstitutionEntity>>();
        var currentTenant = new Mock<ICurrentTenantService>();
        var storageService = new Mock<IStorageService>();
        var redisService = new Mock<IRedisService<ReservEase.Alumni.Institution.Api.Options.InstitutionRedisConfig>>();
        var tokenOptions = Microsoft.Extensions.Options.Options.Create(new BearerTokenConfig { InstitutionSigningKey = "x", Issuer = "x", Audience = "x", AccessTokenLifetime = 1, RefreshTokenLifetime = 1 });

        var notificationActor = new Mock<INotificationActor>();

        var _ = new InstitutionAuthService(adminRepo.Object, redisService.Object, tokenOptions, new NullLogger<InstitutionAuthService>());
        var __ = new InstitutionStaffService(adminRepo.Object, new NullLogger<InstitutionStaffService>());
        var ___ = new CampaignService(campaignRepo.Object, contributionRepo.Object, memberRepo.Object, storageService.Object, notificationActor.Object, new NullLogger<CampaignService>());
        var ____ = new EventService(eventRepo.Object, eventRsvpRepo.Object, memberRepo.Object, storageService.Object, notificationActor.Object, new NullLogger<EventService>());
        var _____ = new ForumService(forumCategoryRepo.Object, forumThreadRepo.Object, new NullLogger<ForumService>());
        var ______ = new JobService(jobRepo.Object, storageService.Object, notificationActor.Object, new NullLogger<JobService>());
        var _______ = new MemberManagementService(memberRepo.Object, campaignRepo.Object, contributionRepo.Object, institutionRepo.Object, currentTenant.Object, new NullLogger<MemberManagementService>());
        var ________ = new MentorshipService(mentorProfileRepo.Object, mentorshipRequestRepo.Object, new NullLogger<MentorshipService>());
        var _________ = new NewsService(newsRepo.Object, adminRepo.Object, storageService.Object, new NullLogger<NewsService>());
        var __________ = new ReportService(memberRepo.Object, contributionRepo.Object, campaignRepo.Object, eventRepo.Object, jobRepo.Object, new NullLogger<ReportService>());
        var ___________ = new ResourceService(resourceRepo.Object, storageService.Object, new NullLogger<ResourceService>());
        var ____________ = new UploadService(storageService.Object, new NullLogger<UploadService>());

        Assert.NotNull(_);
        Assert.NotNull(__);
        Assert.NotNull(___);
        Assert.NotNull(____);
        Assert.NotNull(_____);
        Assert.NotNull(______);
        Assert.NotNull(_______);
        Assert.NotNull(________);
        Assert.NotNull(_________);
        Assert.NotNull(__________);
        Assert.NotNull(___________);
        Assert.NotNull(____________);
    }
}
