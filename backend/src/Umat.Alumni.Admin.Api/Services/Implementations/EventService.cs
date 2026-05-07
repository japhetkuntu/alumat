using Umat.Alumni.Admin.Api.Extensions;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Extensions;
using Umat.Alumni.PostgresDb.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Repositories;
using Umat.Alumni.Storage.Sdk.Services;

namespace Umat.Alumni.Admin.Api.Services.Implementations;

public class EventService(
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<EventRsvp> rsvpRepo,
    IAlumniPgRepository<Member> memberRepo,
    IStorageService storageService,
    ILogger<EventService> logger) : IEventService
{
    public async Task<IApiResponse<PgPagedResult<AlumniEventDto>>> GetEventsAsync(EventFilter filter, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetEvents request — filter: {Filter} (admin: {AdminId})", filter.Serialize(), admin.Id);
            var isSuper = admin.Role == "SuperAdmin";
            var yearGroup = admin.GraduationYear;

            var result = await eventRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "StartDate", filter.SortDir ?? "asc",
                e => (string.IsNullOrEmpty(filter.Status) || e.Status == filter.Status)
                      && (isSuper || e.CreatedBy == admin.Id || (yearGroup.HasValue && e.YearGroups != null && e.YearGroups.Contains(yearGroup.Value))));

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
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving events");
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<AlumniEventDto>>("Failed to retrieve events");
        }
    }

    public async Task<IApiResponse<AlumniEventDto>> GetEventByIdAsync(string eventId, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetEventById request for eventId: {EventId} (admin: {AdminId})", eventId, admin.Id);
            var ev = await eventRepo.GetByIdAsync(eventId);
            if (ev is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<AlumniEventDto>("Event not found");

            if (!admin.CanViewYearGroupScopedItem(ev.YearGroups, ev.CreatedBy))
            {
                logger.LogWarning("Denied event view access for admin {AdminId} to event {EventId} (adminYear={AdminYear}, eventYears={EventYears}, createdBy={CreatedBy})",
                    admin.Id, eventId, admin.GraduationYear, ev.YearGroups ?? new List<int>(), ev.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<AlumniEventDto>("Event not found");
            }

            return ev.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving event {EventId}", eventId);
            return ApiResponseExtensions.ToServerErrorApiResponse<AlumniEventDto>("Failed to retrieve event");
        }
    }

    public async Task<IApiResponse<AlumniEventDto>> CreateEventAsync(CreateEventRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("CreateEvent request: {Request} by admin {AdminId}", request.Serialize(), admin.Id);
            // We currently support only free events via admin flow.
            if (request.IsTicketed)
                return ApiResponseExtensions.ToBadRequestApiResponse<AlumniEventDto>("Paid events are not supported yet. Please create a free event.");
            if (request.TicketPrice.HasValue && request.TicketPrice.Value > 0)
                return ApiResponseExtensions.ToBadRequestApiResponse<AlumniEventDto>("Paid events are not supported yet. Please create a free event.");

            var ev = new AlumniEvent
            {
                Title = request.Title,
                Description = request.Description,
                Venue = request.Venue,
                StartDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc),
                EndDate = request.EndDate.HasValue ? DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc) : (DateTime?)null,
                IsTicketed = false,
                TicketPrice = null,
                GoogleLocationUrl = request.GoogleLocationUrl,
                Capacity = request.Capacity,
                Status = "Upcoming",
                YearGroups = admin.ResolveYearGroupsForCreation(request.YearGroups),
                YoutubeVideoUrls = request.YoutubeVideoUrls,
                CreatedBy = admin.Id,
            };

            if (request.BannerImage is not null)
            {
                var name = $"{Guid.NewGuid():N}{Path.GetExtension(request.BannerImage.FileName)}";
                ev.BannerImageUrl = await storageService.UploadFileAsync(request.BannerImage, name);
            }

            if (request.Images is { Count: > 0 })
                ev.ImageUrls = await storageService.BulkUploadFilesAsync(request.Images);

            await eventRepo.AddAsync(ev);
            logger.LogInformation("Event {EventId} created by admin {AdminId}", ev.Id, admin.Id);
            return ev.ToDto().ToCreatedApiResponse("Event created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating event: {Request} by admin {AdminId}", request.Serialize(), admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<AlumniEventDto>("Failed to create event");
        }
    }

    public async Task<IApiResponse<AlumniEventDto>> UpdateEventAsync(UpdateEventRequest request, AuthData admin)
    {
        try
        {
            logger.LogInformation("UpdateEvent request for eventId: {EventId} by admin {AdminId}", request.EventId, admin.Id);
            var ev = await eventRepo.GetByIdAsync(request.EventId);
            if (ev is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<AlumniEventDto>("Event not found");

            if (!admin.CanModifyYearGroupScopedItem(ev.YearGroups, ev.CreatedBy))
            {
                logger.LogWarning("Denied event update access for admin {AdminId} to event {EventId} (adminYear={AdminYear}, eventYears={EventYears}, createdBy={CreatedBy})",
                    admin.Id, request.EventId, admin.GraduationYear, ev.YearGroups ?? new List<int>(), ev.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<AlumniEventDto>("Event not found");
            }

            if (request.IsTicketed)
                return ApiResponseExtensions.ToBadRequestApiResponse<AlumniEventDto>("Paid events are not supported yet. Please create a free event.");
            if (request.TicketPrice.HasValue && request.TicketPrice.Value > 0)
                return ApiResponseExtensions.ToBadRequestApiResponse<AlumniEventDto>("Paid events are not supported yet. Please create a free event.");

            ev.Title = request.Title;
            ev.Description = request.Description;
            ev.Venue = request.Venue;
            ev.StartDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc);
            ev.EndDate = request.EndDate.HasValue ? DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc) : (DateTime?)null;
            ev.IsTicketed = false;
            ev.TicketPrice = null;
            ev.GoogleLocationUrl = request.GoogleLocationUrl;
            ev.Capacity = request.Capacity;
            ev.Status = request.Status;
            ev.YearGroups = admin.ResolveYearGroupsForCreation(request.YearGroups);
            ev.YoutubeVideoUrls = request.YoutubeVideoUrls;

            if (request.BannerImage is not null)
            {
                var name = $"{Guid.NewGuid():N}{Path.GetExtension(request.BannerImage.FileName)}";
                ev.BannerImageUrl = await storageService.UploadFileAsync(request.BannerImage, name);
            }

            var imageUrls = new List<string>();
            if (request.ExistingImageUrls is { Count: > 0 })
                imageUrls.AddRange(request.ExistingImageUrls);
            if (request.Images is { Count: > 0 })
                imageUrls.AddRange(await storageService.BulkUploadFilesAsync(request.Images));
            ev.ImageUrls = imageUrls.Count > 0 ? imageUrls : null;

            ev.UpdatedAt = DateTime.UtcNow;
            ev.UpdatedBy = admin.Id;
            await eventRepo.UpdateAsync(ev);
            logger.LogInformation("Event {EventId} updated by admin {AdminId}", ev.Id, admin.Id);
            return ev.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating event {EventId} by admin {AdminId}", request.EventId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<AlumniEventDto>("Failed to update event");
        }
    }

    public async Task<IApiResponse<object>> CancelEventAsync(string eventId, AuthData admin)
    {
        try
        {
            logger.LogInformation("CancelEvent request for eventId: {EventId} (admin: {AdminId})", eventId, admin.Id);
            var ev = await eventRepo.GetByIdAsync(eventId);
            if (ev is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Event not found");

            if (!admin.CanModifyYearGroupScopedItem(ev.YearGroups, ev.CreatedBy))
            {
                logger.LogWarning("Denied event cancel access for admin {AdminId} to event {EventId} (adminYear={AdminYear}, eventYears={EventYears}, createdBy={CreatedBy})",
                    admin.Id, eventId, admin.GraduationYear, ev.YearGroups ?? new List<int>(), ev.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Event not found");
            }

            ev.Status = "Cancelled";
            ev.UpdatedAt = DateTime.UtcNow;
            ev.UpdatedBy = admin.Id;
            await eventRepo.UpdateAsync(ev);
            logger.LogInformation("Event {EventId} cancelled", eventId);
            return new object().ToOkApiResponse("Event cancelled");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error cancelling event {EventId}", eventId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to cancel event");
        }
    }

    public async Task<IApiResponse<object>> DeleteEventAsync(string eventId, AuthData admin)
    {
        try
        {
            logger.LogInformation("DeleteEvent request for eventId: {EventId} (admin: {AdminId})", eventId, admin.Id);
            var ev = await eventRepo.GetByIdAsync(eventId);
            if (ev is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Event not found");

            if (!admin.CanModifyYearGroupScopedItem(ev.YearGroups, ev.CreatedBy))
            {
                logger.LogWarning("Denied event delete access for admin {AdminId} to event {EventId} (adminYear={AdminYear}, eventYears={EventYears}, createdBy={CreatedBy})",
                    admin.Id, eventId, admin.GraduationYear, ev.YearGroups ?? new List<int>(), ev.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Event not found");
            }

            await eventRepo.RemoveAsync(ev);
            logger.LogInformation("Event {EventId} deleted", eventId);
            return new object().ToOkApiResponse("Event deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting event {EventId}", eventId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete event");
        }
    }

    public async Task<IApiResponse<PgPagedResult<EventRsvpDto>>> GetRsvpsAsync(string eventId, BaseFilter filter, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetRsvps request for eventId: {EventId} — filter: {Filter} (admin: {AdminId})", eventId, filter.Serialize(), admin.Id);
            var ev = await eventRepo.GetByIdAsync(eventId);
            if (ev is null || !admin.CanViewYearGroupScopedItem(ev.YearGroups, ev.CreatedBy))
            {
                logger.LogWarning("Denied RSVP view access for admin {AdminId} to event {EventId} (adminYear={AdminYear}, eventYears={EventYears}, createdBy={CreatedBy})",
                    admin.Id, eventId, admin.GraduationYear, ev?.YearGroups ?? new List<int>(), ev?.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<PgPagedResult<EventRsvpDto>>("Event not found");
            }

            var statusFilter = filter is Models.EventRsvpFilter f ? f.Status?.Trim() : null;
            var status = string.IsNullOrWhiteSpace(statusFilter) ? "Confirmed" : statusFilter;

            var result = await rsvpRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "CreatedAt", filter.SortDir ?? "desc",
                r => r.EventId == eventId && (status.Equals("All", StringComparison.OrdinalIgnoreCase) || r.Status == status));

            // Backfill missing snapshots for RSVPs stored before jsonb columns were added.
            var needsBackfill = result.Results.Where(r => r.Member is null || r.Event is null).ToList();
            if (needsBackfill.Count > 0)
            {
                var memberIds = needsBackfill.Where(r => r.Member is null).Select(r => r.MemberId).Distinct().ToList();
                var eventIds2 = needsBackfill.Where(r => r.Event is null).Select(r => r.EventId).Distinct().ToList();

                var members = memberIds.Count > 0
                    ? (await memberRepo.GetAllAsync(m => memberIds.Contains(m.Id))).ToDictionary(m => m.Id)
                    : new Dictionary<string, Member>();
                var events = eventIds2.Count > 0
                    ? (await eventRepo.GetAllAsync(e => eventIds2.Contains(e.Id))).ToDictionary(e => e.Id)
                    : new Dictionary<string, AlumniEvent>();

                foreach (var r in needsBackfill)
                {
                    if (r.Member is null && members.TryGetValue(r.MemberId, out var m))
                        r.Member = new MemberSnapshot { Id = m.Id, FirstName = m.FirstName, LastName = m.LastName, Email = m.Email, ProfilePictureUrl = m.ProfilePictureUrl };
                    if (r.Event is null && events.TryGetValue(r.EventId, out var e))
                        r.Event = new EventSnapshot { Id = e.Id, Title = e.Title, Venue = e.Venue };
                }

                await rsvpRepo.UpdateRangeAsync(needsBackfill);
            }

            var dtoResult = new PgPagedResult<EventRsvpDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(r => r.ToDto()).ToList(),
            };
            return dtoResult.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving RSVPs for eventId: {EventId}", eventId);
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<EventRsvpDto>>("Failed to retrieve RSVPs");
        }
    }

    public async Task<IApiResponse<object>> ReopenRsvpAsync(string eventId, string rsvpId, AuthData admin)
    {
        try
        {
            logger.LogInformation("Reopen RSVP request for eventId: {EventId} rsvpId: {RsvpId} (admin: {AdminId})", eventId, rsvpId, admin.Id);
            var ev = await eventRepo.GetByIdAsync(eventId);
            if (ev is null || !admin.CanModifyYearGroupScopedItem(ev.YearGroups, ev.CreatedBy))
            {
                logger.LogWarning("Denied RSVP reopen access for admin {AdminId} to event {EventId} (adminYear={AdminYear}, eventYears={EventYears}, createdBy={CreatedBy})",
                    admin.Id, eventId, admin.GraduationYear, ev?.YearGroups ?? new List<int>(), ev?.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Event not found");
            }

            var rsvp = await rsvpRepo.GetByIdAsync(rsvpId);
            if (rsvp is null || rsvp.EventId != eventId)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("RSVP not found");

            if (rsvp.Status == "Confirmed")
                return ApiResponseExtensions.ToConflictApiResponse<object>("RSVP is already confirmed");

            rsvp.Status = "Confirmed";
            rsvp.UpdatedAt = DateTime.UtcNow;
            await rsvpRepo.UpdateAsync(rsvp);

            ev.RsvpCount += 1;
            await eventRepo.UpdateAsync(ev);

            return new object().ToOkApiResponse("RSVP reopened");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error reopening RSVP for eventId: {EventId} rsvpId: {RsvpId}", eventId, rsvpId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to reopen RSVP");
        }
    }
}

