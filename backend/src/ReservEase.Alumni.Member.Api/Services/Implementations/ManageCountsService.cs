using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

/// <summary>
/// The numbers on a leader's tabs. Each is taken from the same list its tab shows, so a number and the list behind it can never
/// disagree, and a section the leader's roles don't include is simply left out (the guard each list already has says so).
/// </summary>
public class ManageCountsService(
    IManageService people, IManageContentService content, IManageFundraisingService fundraising, ILogger<ManageCountsService> logger) : IManageCountsService
{
    public async Task<IApiResponse<ManageCountsDto>> GetAsync(string memberId, string scopeType, string scopeId)
    {
        try
        {
            // One after another: the lists share a database connection for the request.
            var members = await people.GetPeopleCountsAsync(memberId, scopeType, scopeId);
            var events = await people.GetEventsAsync(memberId, scopeType, scopeId);
            var news = await content.GetNewsAsync(memberId, scopeType, scopeId);
            var jobs = await content.GetJobsAsync(memberId, scopeType, scopeId);
            var links = await content.GetResourcesAsync(memberId, scopeType, scopeId);
            var funds = await fundraising.GetCampaignsAsync(memberId, scopeType, scopeId);

            // A refused list means that section is not theirs; any other failure is a real error, not a missing tab.
            var codes = new[] { members.Code, events.Code, news.Code, jobs.Code, links.Code, funds.Code };
            if (codes.All(c => c != 200 && c != 403)) return ApiResponseExtensions.ToNotFoundApiResponse<ManageCountsDto>("That group was not found");

            var now = DateTime.UtcNow;
            return new ManageCountsDto(
                members.Code == 200 ? members.Data!.Members : null,
                members.Code == 200 ? members.Data!.Waiting : null,
                events.Code == 200 ? events.Data!.Count(e => e.Status != "Cancelled" && (e.EndDate ?? e.StartDate) >= now) : null,
                news.Code == 200 ? news.Data!.Count : null,
                jobs.Code == 200 ? jobs.Data!.Count(j => j.Status != "Closed") : null,
                links.Code == 200 ? links.Data!.Count : null,
                funds.Code == 200 ? funds.Data!.Count(c => c.Status == nameof(CampaignStatus.Active)) : null).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error counting what a group holds {ScopeType}/{ScopeId}", scopeType, scopeId);
            return ApiResponseExtensions.ToServerErrorApiResponse<ManageCountsDto>("Failed to load the counts");
        }
    }
}
