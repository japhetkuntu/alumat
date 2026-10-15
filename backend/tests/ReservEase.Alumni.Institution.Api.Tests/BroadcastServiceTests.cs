using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using Xunit;
using CampaignEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Campaign;
using CampaignStatus = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.CampaignStatus;
using ContributionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Contribution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>
/// Covers the two engagement segments that need cross-entity computation (Dormant is a
/// plain Member-field predicate and isn't separately re-tested here). Repository mocks
/// compile and apply the real predicate against an in-memory list, so these exercise the
/// actual Expression<Func<...>> BuildPredicate builds, not just a hand-written substitute.
/// </summary>
public class BroadcastServiceTests
{
    private static Mock<IAlumniPgRepository<MemberEntity>> MemberRepoReturning(List<MemberEntity> members)
    {
        var repo = new Mock<IAlumniPgRepository<MemberEntity>>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<MemberEntity, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync((Expression<Func<MemberEntity, bool>>? predicate, bool _) =>
                predicate is null ? members : members.Where(predicate.Compile()));
        return repo;
    }

    private static Mock<IAlumniPgRepository<ContributionEntity>> ContributionRepoReturning(List<ContributionEntity> contributions)
    {
        var repo = new Mock<IAlumniPgRepository<ContributionEntity>>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<ContributionEntity, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync((Expression<Func<ContributionEntity, bool>>? predicate, bool _) =>
                predicate is null ? contributions : contributions.Where(predicate.Compile()));
        return repo;
    }

    private static Mock<IAlumniPgRepository<CampaignEntity>> CampaignRepoReturning(List<CampaignEntity> campaigns)
    {
        var repo = new Mock<IAlumniPgRepository<CampaignEntity>>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<CampaignEntity, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync((Expression<Func<CampaignEntity, bool>>? predicate, bool _) =>
                predicate is null ? campaigns : campaigns.Where(predicate.Compile()));
        return repo;
    }

    private static AuthData SuperAdmin() => new() { Id = "admin-1", Role = "SuperAdmin", FirstName = "A", LastName = "Dmin" };

    private static BroadcastService MakeService(
        List<MemberEntity> members, List<ContributionEntity>? contributions = null, List<CampaignEntity>? campaigns = null,
        Mock<IStorageService>? storage = null, ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution? institution = null)
    {
        var memberRepo = MemberRepoReturning(members);
        var contributionRepo = ContributionRepoReturning(contributions ?? []);
        var campaignRepo = CampaignRepoReturning(campaigns ?? []);
        var currentTenant = new Mock<ICurrentTenantService>();
        currentTenant.SetupGet(c => c.InstitutionId).Returns("inst-1");
        currentTenant.SetupGet(c => c.InstitutionSlug).Returns("demo");
        var temporalProvider = Mock.Of<ITemporalClientProvider>(t => t.IsAvailable == false); // never actually reaches Temporal in tests
        // By default the platform has switched every channel on, so tests about audiences are not about channel gating.
        institution ??= new ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution
            { Id = "inst-1", BroadcastEmailAllowed = true, BroadcastSmsAllowed = true, BroadcastWhatsAppAllowed = true };
        var institutionRepo = new Mock<IAlumniPgRepository<ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution>>();
        institutionRepo.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync(institution);

        return new BroadcastService(
            memberRepo.Object, contributionRepo.Object, campaignRepo.Object,
            Mock.Of<IAlumniPgRepository<ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.AudienceSegment>>(),
            institutionRepo.Object,
            new ReservEase.Alumni.PostgresDb.Sdk.Services.AudienceSegmentResolver(memberRepo.Object, Mock.Of<IAlumniPgRepository<ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.CommunityMembership>>(), Mock.Of<IAlumniPgRepository<ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Community>>(), contributionRepo.Object, campaignRepo.Object),
            (storage ?? new Mock<IStorageService>()).Object,
            temporalProvider, currentTenant.Object, new NullLogger<BroadcastService>());
    }

    private static MemberEntity Member(string id, string status = "Active", DateTime? lastLogin = null, int gradYear = 2020) =>
        new() { Id = id, Email = $"{id}@example.com", FirstName = id, Status = status, LastLoginAt = lastLogin, GraduationYear = gradYear };

    [Fact]
    public async Task Dormant_ExcludesMembersWhoLoggedInRecently()
    {
        var members = new List<MemberEntity>
        {
            Member("recent", lastLogin: DateTime.UtcNow.AddDays(-5)),
            Member("stale", lastLogin: DateTime.UtcNow.AddDays(-90)),
            Member("never", lastLogin: null),
        };
        var service = MakeService(members);

        var count = await service.GetRecipientCountAsync(new BroadcastFilter { EngagementSegment = EngagementSegments.Dormant }, SuperAdmin());

        Assert.Equal(200, count.Code);
        Assert.Equal(2, count.Data); // "stale" and "never", not "recent"
    }

    [Fact]
    public async Task NoContributionsEver_ExcludesMembersWithAnySuccessfulPayment()
    {
        var members = new List<MemberEntity> { Member("paid"), Member("unpaid"), Member("rejected-only") };
        var contributions = new List<ContributionEntity>
        {
            new() { MemberId = "paid", CampaignId = "c1", Status = "Successful" },
            new() { MemberId = "rejected-only", CampaignId = "c1", Status = "Rejected" },
        };
        var service = MakeService(members, contributions);

        var count = await service.GetRecipientCountAsync(new BroadcastFilter { EngagementSegment = EngagementSegments.NoContributionsEver }, SuperAdmin());

        Assert.Equal(200, count.Code);
        Assert.Equal(2, count.Data); // "unpaid" and "rejected-only" (a rejected payment doesn't count), not "paid"
    }

    [Fact]
    public async Task NoContributionToActiveFundraiser_IsEmpty_WhenNoFundraiserIsOpen()
    {
        var members = new List<MemberEntity> { Member("a"), Member("b") };
        // Only a membership campaign and a closed fundraiser exist — neither counts as "open".
        var campaigns = new List<CampaignEntity>
        {
            new() { Id = "dues", IsMembershipCampaign = true, Status = CampaignStatus.Active, Deadline = DateTime.UtcNow.AddDays(30) },
            new() { Id = "closed-fundraiser", IsMembershipCampaign = false, Status = CampaignStatus.Closed, Deadline = DateTime.UtcNow.AddDays(-1) },
        };
        var service = MakeService(members, campaigns: campaigns);

        var count = await service.GetRecipientCountAsync(new BroadcastFilter { EngagementSegment = EngagementSegments.NoContributionToActiveFundraiser }, SuperAdmin());

        Assert.Equal(200, count.Code);
        Assert.Equal(0, count.Data); // meaningless with nothing open — must not silently mean "everyone"
    }

    [Fact]
    public async Task NoContributionToActiveFundraiser_ExcludesOnlyThoseWhoGaveToTheOpenOne()
    {
        var members = new List<MemberEntity> { Member("gave"), Member("didnt-give"), Member("gave-to-dues-only") };
        var campaigns = new List<CampaignEntity>
        {
            new() { Id = "open-fundraiser", IsMembershipCampaign = false, Status = CampaignStatus.Active, Deadline = DateTime.UtcNow.AddDays(10) },
            new() { Id = "dues", IsMembershipCampaign = true, Status = CampaignStatus.Active, Deadline = DateTime.UtcNow.AddDays(10) },
        };
        var contributions = new List<ContributionEntity>
        {
            new() { MemberId = "gave", CampaignId = "open-fundraiser", Status = "Successful" },
            new() { MemberId = "gave-to-dues-only", CampaignId = "dues", Status = "Successful" },
        };
        var service = MakeService(members, contributions, campaigns);

        var count = await service.GetRecipientCountAsync(new BroadcastFilter { EngagementSegment = EngagementSegments.NoContributionToActiveFundraiser }, SuperAdmin());

        Assert.Equal(200, count.Code);
        // "didnt-give" and "gave-to-dues-only" (dues isn't the open fundraiser) — not "gave"
        Assert.Equal(2, count.Data);
    }

    [Fact]
    public async Task SendBroadcastAsync_UploadsImage_AndReturnsChannelsAndCount()
    {
        var members = new List<MemberEntity> { Member("m1"), Member("m2") };
        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.UploadFileAsync(It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://cdn.example.com/broadcast-image.png");
        var service = MakeService(members, storage: storage);

        var image = new Mock<Microsoft.AspNetCore.Http.IFormFile>();
        image.SetupGet(f => f.FileName).Returns("banner.png");

        var result = await service.SendBroadcastAsync(new SendBroadcastRequest
        {
            Message = "Come back and see what's new!",
            Channels = ["InApp", "Email"],
            Image = image.Object,
        }, SuperAdmin());

        Assert.Equal(200, result.Code);
        Assert.Equal(2, result.Data!.RecipientCount);
        Assert.Equal(new[] { "InApp", "Email" }, result.Data.Channels);
        storage.Verify(s => s.UploadFileAsync(image.Object, It.IsAny<string>(), It.IsAny<string>(), "demo"), Times.Once);
    }

    [Fact]
    public async Task SendBroadcastAsync_RequiresMessage()
    {
        var service = MakeService([Member("m1")]);

        var result = await service.SendBroadcastAsync(new SendBroadcastRequest { Message = "  " }, SuperAdmin());

        Assert.Equal(400, result.Code);
    }

    [Fact]
    public async Task Sms_broadcast_is_refused_until_the_platform_switches_it_on()
    {
        var off = new ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution { Id = "inst-1", BroadcastEmailAllowed = true, BroadcastSmsAllowed = false };
        var service = MakeService([Member("a")], institution: off);

        var result = await service.SendBroadcastAsync(new SendBroadcastRequest { Message = "Hello", Channels = ["InApp", "Sms"] }, SuperAdmin());

        Assert.Equal(400, result.Code);
        Assert.Contains("SMS", result.Message);
    }

    [Fact]
    public async Task In_app_always_goes_through_even_when_nothing_else_is_switched_on()
    {
        var off = new ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution { Id = "inst-1" };
        var service = MakeService([Member("a")], institution: off);

        var result = await service.SendBroadcastAsync(new SendBroadcastRequest { Message = "Hello", Channels = ["InApp"] }, SuperAdmin());

        Assert.Equal(200, result.Code);
    }

    [Fact]
    public async Task The_channels_endpoint_reports_what_the_platform_has_allowed()
    {
        var some = new ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution { Id = "inst-1", BroadcastEmailAllowed = true };
        var service = MakeService([], institution: some);

        var c = (await service.GetChannelsAsync()).Data!;

        Assert.True(c.InApp);
        Assert.True(c.Email);
        Assert.False(c.Sms);
        Assert.False(c.WhatsApp);
    }
}
