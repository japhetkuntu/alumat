using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.Member.Api.Services.Interfaces;

namespace ReservEase.Alumni.Member.Api.Tests;

/// <summary>The numbers on a leader's tabs: taken from the lists the tabs show, only for the sections the leader may open.</summary>
public class ManageCountsServiceTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static GroupEventDto Event(string status, DateTime start, DateTime? end = null) =>
        new("e", "Title", null, "Hall", start, end, null, 0, status, true);

    private static IApiResponse<T> Refused<T>() => ApiResponseExtensions.ToForbiddenApiResponse<T>("No");

    private sealed class Rig
    {
        public Mock<IManageService> People { get; } = new();
        public Mock<IManageContentService> Content { get; } = new();
        public Mock<IManageFundraisingService> Fundraising { get; } = new();

        public Rig()
        {
            // Everything refused until a test opens a section.
            People.Setup(p => p.GetPeopleCountsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(Refused<GroupPeopleCountsDto>());
            People.Setup(p => p.GetEventsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(Refused<List<GroupEventDto>>());
            Content.Setup(c => c.GetNewsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(Refused<List<GroupNewsDto>>());
            Content.Setup(c => c.GetJobsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(Refused<List<GroupJobDto>>());
            Content.Setup(c => c.GetResourcesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(Refused<List<GroupResourceDto>>());
            Fundraising.Setup(f => f.GetCampaignsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(Refused<List<GroupCampaignDto>>());
        }

        public ManageCountsService Service => new(People.Object, Content.Object, Fundraising.Object, NullLogger<ManageCountsService>.Instance);
    }

    [Fact]
    public async Task A_leader_with_every_privilege_gets_a_number_for_every_section()
    {
        var rig = new Rig();
        rig.People.Setup(p => p.GetPeopleCountsAsync("m", "Batch", "b")).ReturnsAsync(new GroupPeopleCountsDto(48, 3).ToOkApiResponse());
        rig.People.Setup(p => p.GetEventsAsync("m", "Batch", "b")).ReturnsAsync(new List<GroupEventDto> { Event("Upcoming", Now.AddDays(2)), Event("Upcoming", Now.AddDays(9)) }.ToOkApiResponse());
        rig.Content.Setup(c => c.GetNewsAsync("m", "Batch", "b")).ReturnsAsync(new List<GroupNewsDto> { new("n1", "T", "C", "General", Now, true), new("n2", "T", "C", "General", Now, true), new("n3", "T", "C", "General", Now, true) }.ToOkApiResponse());
        rig.Content.Setup(c => c.GetJobsAsync("m", "Batch", "b")).ReturnsAsync(new List<GroupJobDto> { new("j", "T", "Co", "Accra", "Full-time", null, null, null, "Active", true) }.ToOkApiResponse());
        rig.Content.Setup(c => c.GetResourcesAsync("m", "Batch", "b")).ReturnsAsync(new List<GroupResourceDto> { new("r", "T", null, "Guides", "https://x.test", true), new("r2", "T", null, "Guides", "https://y.test", true) }.ToOkApiResponse());
        rig.Fundraising.Setup(f => f.GetCampaignsAsync("m", "Batch", "b")).ReturnsAsync(new List<GroupCampaignDto> { Campaign("Active") }.ToOkApiResponse());

        var counts = (await rig.Service.GetAsync("m", "Batch", "b")).Data!;

        Assert.Equal((48, 3, 2, 3, 1, 2, 1), (counts.Members, counts.Waiting, counts.Events, counts.News, counts.Jobs, counts.Links, counts.Fundraisers));
    }

    [Fact]
    public async Task A_section_the_leaders_roles_dont_include_has_no_number()
    {
        var rig = new Rig();
        rig.Content.Setup(c => c.GetNewsAsync("m", "Batch", "b")).ReturnsAsync(new List<GroupNewsDto> { new("n1", "T", "C", "General", Now, true) }.ToOkApiResponse());

        var counts = (await rig.Service.GetAsync("m", "Batch", "b")).Data!;

        Assert.Equal(1, counts.News);
        Assert.Null(counts.Members); Assert.Null(counts.Waiting); Assert.Null(counts.Events); Assert.Null(counts.Jobs); Assert.Null(counts.Links); Assert.Null(counts.Fundraisers);
    }

    [Fact]
    public async Task Events_that_are_over_or_cancelled_jobs_that_are_closed_and_fundraisers_that_have_ended_are_not_counted()
    {
        var rig = new Rig();
        rig.People.Setup(p => p.GetEventsAsync("m", "Batch", "b")).ReturnsAsync(new List<GroupEventDto>
        {
            Event("Upcoming", Now.AddDays(3)),                          // counts
            Event("Upcoming", Now.AddDays(-2), Now.AddDays(1)),         // began, still going: counts
            Event("Upcoming", Now.AddDays(-5)),                         // over
            Event("Cancelled", Now.AddDays(4)),                         // cancelled
        }.ToOkApiResponse());
        rig.Content.Setup(c => c.GetJobsAsync("m", "Batch", "b")).ReturnsAsync(new List<GroupJobDto>
        {
            new("j1", "T", "Co", "Accra", "Full-time", null, null, null, "Active", true), new("j2", "T", "Co", "Accra", "Full-time", null, null, null, "Closed", false),
        }.ToOkApiResponse());
        rig.Fundraising.Setup(f => f.GetCampaignsAsync("m", "Batch", "b")).ReturnsAsync(new List<GroupCampaignDto> { Campaign("Active"), Campaign("Closed"), Campaign("Completed") }.ToOkApiResponse());

        var counts = (await rig.Service.GetAsync("m", "Batch", "b")).Data!;

        Assert.Equal((2, 1, 1), (counts.Events, counts.Jobs, counts.Fundraisers));
    }

    [Fact]
    public async Task A_group_the_member_has_no_part_in_is_not_found()
    {
        var rig = new Rig();
        rig.People.Setup(p => p.GetPeopleCountsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(ApiResponseExtensions.ToNotFoundApiResponse<GroupPeopleCountsDto>("x"));
        rig.People.Setup(p => p.GetEventsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(ApiResponseExtensions.ToNotFoundApiResponse<List<GroupEventDto>>("x"));
        rig.Content.Setup(c => c.GetNewsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(ApiResponseExtensions.ToNotFoundApiResponse<List<GroupNewsDto>>("x"));
        rig.Content.Setup(c => c.GetJobsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(ApiResponseExtensions.ToNotFoundApiResponse<List<GroupJobDto>>("x"));
        rig.Content.Setup(c => c.GetResourcesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(ApiResponseExtensions.ToNotFoundApiResponse<List<GroupResourceDto>>("x"));
        rig.Fundraising.Setup(f => f.GetCampaignsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(ApiResponseExtensions.ToNotFoundApiResponse<List<GroupCampaignDto>>("x"));

        Assert.Equal(404, (await rig.Service.GetAsync("m", "Batch", "nope")).Code);
    }

    private static GroupCampaignDto Campaign(string status) => new("c", "T", null, 100, 0, Now.AddDays(10), status, 0, 0, true, true);
}
