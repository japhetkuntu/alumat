using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class MemberEventUpdateService(
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<EventUpdate> updateRepo,
    IAlumniPgRepository<EventRsvp> rsvpRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    ILogger<MemberEventUpdateService> logger) : IMemberEventUpdateService
{
    public async Task<IApiResponse<EventUpdatesResponse>> GetAsync(string eventId, AuthData member)
    {
        try
        {
            var ev = await eventRepo.GetByIdAsync(eventId);
            if (ev is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<EventUpdatesResponse>("Event not found");
            if (!string.IsNullOrEmpty(ev.CommunityId))
            {
                var membership = await membershipRepo.GetOneAsync(m => m.CommunityId == ev.CommunityId && m.MemberId == member.Id);
                if (membership is not { Status: "Approved" })
                    return ApiResponseExtensions.ToForbiddenApiResponse<EventUpdatesResponse>("You must be an approved member of this community to see this event");
            }

            var updates = EventUpdateRules.InDisplayOrder(await updateRepo.GetAllAsync(u => u.EventId == eventId));

            // A seat is a confirmed RSVP or a paid ticket (both are a Confirmed row). Anything else, including a ticket still being paid for, is outside.
            var hasSeat = await rsvpRepo.CountAsync(r => r.EventId == eventId && r.MemberId == member.Id && r.Status == EventRsvpStatuses.Confirmed) > 0;

            var response = new EventUpdatesResponse
            {
                Locked = !hasSeat,
                Count = updates.Count,
                Categories = updates.GroupBy(u => u.Category)
                    .Select(g => new EventUpdateCategoryCount { Category = g.Key, Count = g.Count() })
                    .OrderByDescending(c => c.Count).ThenBy(c => c.Category).ToList(),
                // Content is only ever built for people with a seat, so a locked response cannot leak any of it.
                Updates = hasSeat ? updates.Select(u => u.ToDto()).ToList() : [],
            };
            return response.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error loading updates for event {EventId}, member {MemberId}", eventId, member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<EventUpdatesResponse>("Failed to load updates");
        }
    }
}
