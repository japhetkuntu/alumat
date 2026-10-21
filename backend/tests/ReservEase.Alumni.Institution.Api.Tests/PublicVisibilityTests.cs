using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Redis.Sdk.Services;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Member.Api.Controllers;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>Per-record public visibility: a record is shown in full to guests only when an admin marked it and it is for everyone.</summary>
public class PublicVisibilityTests
{
    private const string Tenant = "inst-1";

    [Theory]
    [InlineData(true, null, null, true)]
    [InlineData(true, "", new int[0], true)]
    [InlineData(false, null, null, false)]
    [InlineData(true, "group-1", null, false)]
    [InlineData(true, null, new[] { 2019 }, false)]
    public void OnlyInstitutionWideRecordsMarkedPublicAreFullyPublic(bool isPublic, string? communityId, int[]? years, bool expected)
        => Assert.Equal(expected, PublicVisibility.IsFullyPublic(isPublic, communityId, years));

    private static PublicController Controller(AlumniDbContext db)
    {
        var constructor = typeof(PublicController).GetConstructors().Single();
        var emptyMock = typeof(Mock).GetMethods().Single(m => m.Name == "Of" && m.GetParameters().Length == 0);
        var args = constructor.GetParameters().Select(p =>
            p.ParameterType == typeof(IAlumniPgRepository<NewsPost>) ? new AlumniPgRepository<NewsPost>(db) :
            p.ParameterType == typeof(IAlumniPgRepository<AlumniEvent>) ? new AlumniPgRepository<AlumniEvent>(db) :
            p.ParameterType == typeof(IAlumniPgRepository<Job>) ? new AlumniPgRepository<Job>(db) :
            p.ParameterType == typeof(IAlumniPgRepository<Community>) ? new AlumniPgRepository<Community>(db) :
            emptyMock.MakeGenericMethod(p.ParameterType).Invoke(null, null)).ToArray();
        var controller = (PublicController)constructor.Invoke(args);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static ReservEase.Alumni.Member.Api.Controllers.PublicPreviewResponse Preview(IActionResult result)
        => ((ReservEase.Alumni.Common.Sdk.Models.ApiResponse<ReservEase.Alumni.Member.Api.Controllers.PublicPreviewResponse>)((OkObjectResult)result).Value!).Data!;

    private static NewsPost Post(bool isPublic, string? communityId = null, List<int>? years = null) => new()
    {
        Id = Guid.NewGuid().ToString("N"), InstitutionId = Tenant, Title = "Homecoming", Content = "<p>The <strong>full</strong> story.</p>",
        Category = "News", Status = "Published", PublishedAt = DateTime.UtcNow, IsPublic = isPublic, CommunityId = communityId, YearGroups = years,
    };

    [Fact]
    public async Task APublicStory_GivesGuestsTheFullText()
    {
        var name = TestDb.NewName();
        var post = Post(isPublic: true);
        using (var seed = TestDb.Create(name, Tenant)) { seed.NewsPosts.Add(post); seed.SaveChanges(); }
        var preview = Preview(await Controller(TestDb.Create(name, Tenant)).GetPublicPreview("news", post.Id));
        Assert.True(preview.IsPublic);
        Assert.Contains("full", preview.Html);
    }

    [Fact]
    public async Task AStoryNotMarkedPublic_GivesGuestsOnlyAShortPreview()
    {
        var name = TestDb.NewName();
        var post = Post(isPublic: false);
        using (var seed = TestDb.Create(name, Tenant)) { seed.NewsPosts.Add(post); seed.SaveChanges(); }
        var preview = Preview(await Controller(TestDb.Create(name, Tenant)).GetPublicPreview("news", post.Id));
        Assert.False(preview.IsPublic);
        Assert.Null(preview.Html);
        Assert.Equal("Homecoming", preview.Title);
    }

    [Fact]
    public async Task AStoryForYearGroups_StaysClosedEvenIfFlaggedPublic()
    {
        var name = TestDb.NewName();
        var post = Post(isPublic: true, years: [2019]);
        using (var seed = TestDb.Create(name, Tenant)) { seed.NewsPosts.Add(post); seed.SaveChanges(); }
        var preview = Preview(await Controller(TestDb.Create(name, Tenant)).GetPublicPreview("news", post.Id));
        Assert.True(preview.Restricted);
        Assert.False(preview.IsPublic);
        Assert.Null(preview.Html);
        Assert.Null(preview.Description);
    }

    [Fact]
    public async Task AGroupNotMarkedPublic_ShowsGuestsOnlyItsName()
    {
        var name = TestDb.NewName();
        var group = new Community { Id = "g1", InstitutionId = Tenant, Name = "Kumasi Chapter", Description = "<p>Private details</p>", IsActive = true, IsPublic = false };
        using (var seed = TestDb.Create(name, Tenant)) { seed.Communities.Add(group); seed.SaveChanges(); }
        var preview = Preview(await Controller(TestDb.Create(name, Tenant)).GetPublicPreview("community", "g1"));
        Assert.Equal("Kumasi Chapter", preview.Title);
        Assert.True(preview.Restricted);
        Assert.Null(preview.Description);
    }

    private static CommunityService Groups(AlumniDbContext db) => new(
        new AlumniPgRepository<Community>(db), new AlumniPgRepository<CommunityMembership>(db), new AlumniPgRepository<MemberEntity>(db),
        new AlumniPgRepository<Referral>(db), new AlumniPgRepository<Notification>(db), TestDb.Tenant(Tenant, "umat"),
        Mock.Of<IRedisService<PublicContentCacheConfig>>(), Mock.Of<IInstitutionAuditLogService>(), NullLogger<CommunityService>.Instance);

    [Fact]
    public async Task EditingAGroupWithoutSayingKeepsItsPublicSetting_AndAnExplicitChoiceChangesIt()
    {
        var name = TestDb.NewName();
        using (var seed = TestDb.Create(name, Tenant)) { seed.Communities.Add(new Community { Id = "g1", InstitutionId = Tenant, Name = "Chapter", IsActive = true, IsPublic = true }); seed.SaveChanges(); }

        // The activate/deactivate toggle sends no IsPublic; it must not hide a group the admin chose to show.
        await Groups(TestDb.Create(name, Tenant)).UpdateCommunityAsync("g1", new UpdateCommunityRequest { Name = "Chapter", IsActive = true }, "admin1");
        using (var check = TestDb.Create(name, Tenant)) Assert.True(check.Communities.Single(c => c.Id == "g1").IsPublic);

        await Groups(TestDb.Create(name, Tenant)).UpdateCommunityAsync("g1", new UpdateCommunityRequest { Name = "Chapter", IsActive = true, IsPublic = false }, "admin1");
        using (var check = TestDb.Create(name, Tenant)) Assert.False(check.Communities.Single(c => c.Id == "g1").IsPublic);
    }
}
