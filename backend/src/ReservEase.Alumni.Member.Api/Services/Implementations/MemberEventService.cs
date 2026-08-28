using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Extensions;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class MemberEventService(
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<EventRsvp> rsvpRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    IAlumniPgRepository<Community> communityRepo,
    ILogger<MemberEventService> logger) : IMemberEventService
{
    private async Task<bool> IsApprovedCommunityMemberAsync(string communityId, string memberId)
    {
        var membership = await membershipRepo.GetOneAsync(m => m.CommunityId == communityId && m.MemberId == memberId);
        return membership is not null && membership.Status == "Approved";
    }

    private async Task<List<string>> GetApprovedCommunityIdsAsync(string memberId)
    {
        var memberships = await membershipRepo.GetAllAsync(m => m.MemberId == memberId && m.Status == "Approved");
        return memberships.Select(m => m.CommunityId).ToList();
    }

    private async Task EnrichCommunityNamesAsync(IEnumerable<AlumniEventDto> results)
    {
        var communityIds = results.Where(d => d.CommunityId != null).Select(d => d.CommunityId!).Distinct().ToList();
        if (communityIds.Count == 0) return;
        var communities = await communityRepo.GetAllAsync(c => communityIds.Contains(c.Id));
        var nameById = communities.ToDictionary(c => c.Id, c => c.Name);
        foreach (var dto in results)
            if (dto.CommunityId != null && nameById.TryGetValue(dto.CommunityId, out var name))
                dto.CommunityName = name;
    }

    public async Task<IApiResponse<PgPagedResult<AlumniEventDto>>> GetEventsAsync(EventFilter filter, string memberId)
    {
        try
        {
            logger.LogInformation("GetEvents request — filter: {Filter} for member {MemberId}", filter.Serialize(), memberId);

            if (!string.IsNullOrEmpty(filter.CommunityId) && !await IsApprovedCommunityMemberAsync(filter.CommunityId, memberId))
                return ApiResponseExtensions.ToForbiddenApiResponse<PgPagedResult<AlumniEventDto>>("You must be an approved member of this community to view its events");

            var member = await memberRepo.GetByIdAsync(memberId);
            var memberYear = member?.GraduationYear;

            var status = string.IsNullOrWhiteSpace(filter.Status) ? "Upcoming" : filter.Status;
            var includeCancelled = status.Equals("All", StringComparison.OrdinalIgnoreCase);

            var approvedCommunityIds = string.IsNullOrEmpty(filter.CommunityId) ? await GetApprovedCommunityIdsAsync(memberId) : [];

            var result = await eventRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "StartDate", filter.SortDir ?? "asc",
                e => (includeCancelled || e.Status == status)
                      && (string.IsNullOrEmpty(filter.CommunityId)
                          ? (e.CommunityId == null || approvedCommunityIds.Contains(e.CommunityId))
                          : e.CommunityId == filter.CommunityId)
                      && (e.YearGroups == null || e.YearGroups.Count == 0 || (memberYear.HasValue && e.YearGroups.Contains(memberYear.Value))));

            // Keep rsvp counts in sync by recalculating from confirmed RSVPs.
            var eventIds = result.Results.Select(e => e.Id).ToList();
            if (eventIds.Any())
            {
                var rsvpCounts = (await rsvpRepo.GetAllAsync(r => eventIds.Contains(r.EventId) && r.Status == "Confirmed"))
                    .GroupBy(r => r.EventId)
                    .ToDictionary(g => g.Key, g => g.Count());

                foreach (var ev in result.Results)
                {
                    ev.RsvpCount = rsvpCounts.TryGetValue(ev.Id, out var count) ? count : 0;
                }
            }

            var dtoResult = new PgPagedResult<AlumniEventDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(e => e.ToDto()).ToList(),
            };
            await EnrichCommunityNamesAsync(dtoResult.Results);
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving events");
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<AlumniEventDto>>("Failed to retrieve events");
        }
    }

    public async Task<IApiResponse<AlumniEventDto>> GetEventByIdAsync(string eventId)
    {
        try
        {
            logger.LogInformation("GetEventById request — eventId: {EventId}", eventId);
            var ev = await eventRepo.GetByIdAsync(eventId);
            if (ev is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<AlumniEventDto>("Event not found");

            // Recalculate RSVP count to ensure accurate attendance numbers.
            var confirmedRsvps = await rsvpRepo.CountAsync(r => r.EventId == eventId && r.Status == "Confirmed");
            ev.RsvpCount = confirmedRsvps;

            return ev.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving event {EventId}", eventId);
            return ApiResponseExtensions.ToServerErrorApiResponse<AlumniEventDto>("Failed to retrieve event");
        }
    }

    public async Task<IApiResponse<object>> RsvpAsync(RsvpRequest request, AuthData member)
    {
        try
        {
            logger.LogInformation("RSVP request: {Request} by member {MemberId}", request.Serialize(), member.Id);

            var ev = await eventRepo.GetByIdAsync(request.EventId);
            if (ev is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Event not found");
            if (!string.IsNullOrEmpty(ev.CommunityId) && !await IsApprovedCommunityMemberAsync(ev.CommunityId, member.Id))
                return ApiResponseExtensions.ToForbiddenApiResponse<object>("You must be an approved member of this community to RSVP");

            var existing = await rsvpRepo.GetOneAsync(r => r.EventId == request.EventId && r.MemberId == member.Id);
            if (existing is not null)
            {
                if (existing.Status == "Confirmed")
                    return ApiResponseExtensions.ToConflictApiResponse<object>("Already RSVP'd for this event");

                // Re-confirming a previously cancelled RSVP should count again.
                existing.Status = "Confirmed";
                await rsvpRepo.UpdateAsync(existing);

                ev.RsvpCount = await rsvpRepo.CountAsync(r => r.EventId == request.EventId && r.Status == "Confirmed");
                await eventRepo.UpdateAsync(ev);

                return new object().ToOkApiResponse("RSVP re-confirmed");
            }

            var rsvp = new EventRsvp
            {
                EventId = request.EventId,
                Event = new EventSnapshot { Id = ev.Id, Title = ev.Title, Venue = ev.Venue },
                MemberId = member.Id,
                Member = new MemberSnapshot { Id = member.Id, FirstName = member.FirstName, LastName = member.LastName, Email = member.Email, ProfilePictureUrl = member.ProfilePictureUrl },
                Status = "Confirmed",
                CreatedBy = member.Id,
            };
            await rsvpRepo.AddAsync(rsvp);

            ev.RsvpCount = await rsvpRepo.CountAsync(r => r.EventId == request.EventId && r.Status == "Confirmed");
            await eventRepo.UpdateAsync(ev);

            logger.LogInformation("RSVP confirmed for member {MemberId}, event {EventId}", member.Id, request.EventId);
            return new object().ToCreatedApiResponse("RSVP confirmed");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error processing RSVP for member {MemberId}, event {EventId}", member.Id, request.EventId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to process RSVP");
        }
    }

    public async Task<IApiResponse<object>> CancelRsvpAsync(string eventId, AuthData member)
    {
        try
        {
            logger.LogInformation("CancelRsvp request for eventId: {EventId} by member {MemberId}", eventId, member.Id);

            var rsvp = await rsvpRepo.GetOneAsync(r => r.EventId == eventId && r.MemberId == member.Id);
            if (rsvp is null || rsvp.Status != "Confirmed")
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("RSVP not found");

            rsvp.Status = "Cancelled";
            rsvp.UpdatedAt = DateTime.UtcNow;
            await rsvpRepo.UpdateAsync(rsvp);

            var ev = await eventRepo.GetByIdAsync(eventId);
            if (ev is not null)
            {
                ev.RsvpCount = await rsvpRepo.CountAsync(r => r.EventId == eventId && r.Status == "Confirmed");
                await eventRepo.UpdateAsync(ev);
            }

            logger.LogInformation("RSVP cancelled for member {MemberId}, event {EventId}", member.Id, eventId);
            return new object().ToOkApiResponse("RSVP cancelled");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error cancelling RSVP for member {MemberId}, event {EventId}", member.Id, eventId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to cancel RSVP");
        }
    }

    public async Task<IApiResponse<IEnumerable<EventRsvpDto>>> GetMyRsvpsAsync(string memberId, string? status = null)
    {
        try
        {
            logger.LogInformation("GetMyRsvps for member {MemberId} (status={Status})", memberId, status);

            var desiredStatus = string.IsNullOrWhiteSpace(status) ? "Confirmed" : status;
            var rsvps = (await rsvpRepo.GetAllAsync(r => r.MemberId == memberId
                                                       && (desiredStatus.Equals("All", StringComparison.OrdinalIgnoreCase)
                                                           || r.Status == desiredStatus))).ToList();

            // Backfill missing snapshots for RSVPs stored before jsonb columns were added.
            var needsBackfill = rsvps.Where(r => r.Member is null || r.Event is null).ToList();
            if (needsBackfill.Count > 0)
            {
                var member = await memberRepo.GetByIdAsync(memberId);
                var eventIds = needsBackfill.Where(r => r.Event is null).Select(r => r.EventId).Distinct().ToList();
                var events = eventIds.Count > 0
                    ? (await eventRepo.GetAllAsync(e => eventIds.Contains(e.Id))).ToDictionary(e => e.Id)
                    : new Dictionary<string, AlumniEvent>();

                foreach (var r in needsBackfill)
                {
                    if (r.Member is null && member is not null)
                        r.Member = new MemberSnapshot { Id = member.Id, FirstName = member.FirstName, LastName = member.LastName, Email = member.Email, ProfilePictureUrl = member.ProfilePictureUrl };
                    if (r.Event is null && events.TryGetValue(r.EventId, out var e))
                        r.Event = new EventSnapshot { Id = e.Id, Title = e.Title, Venue = e.Venue };
                }

                await rsvpRepo.UpdateRangeAsync(needsBackfill);
            }

            return rsvps.Select(r => r.ToDto()).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving RSVPs for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<IEnumerable<EventRsvpDto>>("Failed to retrieve RSVPs");
        }
    }
}
