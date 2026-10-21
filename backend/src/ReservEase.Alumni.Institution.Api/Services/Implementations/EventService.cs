using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Text;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class EventService(
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<EventRsvp> rsvpRepo,
    IAlumniPgRepository<Member> memberRepo,
    IAlumniPgRepository<InstitutionEntity> institutionRepo,
    IAlumniPgRepository<EventUpdate> updateRepo,
    IStorageService storageService,
    ITemporalClientProvider temporalProvider,
    ICurrentTenantService currentTenant,
    IRedisService<PublicContentCacheConfig> publicCache,
    IInstitutionAuditLogService auditLog,
    ILogger<EventService> logger) : IEventService
{
    private static AlumniEventDto WithSales(AlumniEventDto dto, Dictionary<string, (int Count, decimal Amount)> sales)
    {
        if (!dto.IsTicketed) return dto;
        var found = sales.TryGetValue(dto.Id, out var s);
        dto.PaidTicketCount = found ? s.Count : 0;
        dto.TicketAmountCollected = found ? s.Amount : 0m;
        return dto;
    }

    private Task<int> CountPaidTicketsAsync(string eventId) =>
        rsvpRepo.CountAsync(r => r.EventId == eventId && r.Status == EventRsvpStatuses.Confirmed && r.AmountPaid > 0);

    private async Task<string?> ValidatePricingAsync(bool isTicketed, decimal? ticketPrice, AlumniEvent? existing)
    {
        var institution = currentTenant.InstitutionId is { } id ? await institutionRepo.GetByIdAsync(id) : null;
        var paidSold = existing is null ? 0 : await CountPaidTicketsAsync(existing.Id);
        var confirmed = existing is null ? 0 : await rsvpRepo.CountAsync(r => r.EventId == existing.Id && r.Status == EventRsvpStatuses.Confirmed);
        return EventTicketRules.ValidatePricing(isTicketed, ticketPrice, existing,
            institution?.PaidEventsEnabled == true, !string.IsNullOrWhiteSpace(institution?.PaystackSubaccountCode), paidSold, confirmed);
    }

    /// <summary>What the event form needs to know: whether this community may charge, and whether payouts are ready.</summary>
    public async Task<IApiResponse<EventTicketingDto>> GetTicketingAsync()
    {
        var institution = currentTenant.InstitutionId is { } id ? await institutionRepo.GetByIdAsync(id) : null;
        return new EventTicketingDto(institution?.PaidEventsEnabled == true, !string.IsNullOrWhiteSpace(institution?.PaystackSubaccountCode))
            .ToOkApiResponse();
    }

    /// <summary>
    /// The link on an event is a map or, for an online event, a meeting link. Empty means none; anything else must be a web address, so a
    /// saved link can never be something that runs code when a member taps it. Returns an error to show, or null when it is fine.
    /// </summary>
    internal static string? CleanLocationLink(string? raw, out string? cleaned)
    {
        cleaned = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        if (cleaned is null) return null;
        if (cleaned.Length > 1000 || !EventUpdateRules.IsWebUrl(cleaned, out _))
            return "The map or meeting link must be a web address starting with http:// or https://.";
        return null;
    }

    private Task InvalidatePublicEventsCacheAsync()
        => currentTenant.InstitutionId is { } id ? publicCache.RemoveAsync(PublicContentCacheKeys.Events(id)) : Task.CompletedTask;

    public async Task<IApiResponse<PgPagedResult<AlumniEventDto>>> GetEventsAsync(EventFilter filter, AuthData admin)
    {
        try
        {
            logger.LogInformation("GetEvents request — filter: {Filter} (admin: {AdminId})", filter.Serialize(), admin.Id);
            var isSuper = admin.Role != StaffRoles.ScopedAdmin;
            var yearGroups = admin.YearGroups ?? new List<int>();
            var communityIds = admin.CommunityIds ?? new List<string>();

            var result = await eventRepo.GetPagedAsync(
                filter.Page, filter.PageSize, filter.SortColumn ?? "StartDate", filter.SortDir ?? "asc",
                e => (string.IsNullOrEmpty(filter.Status) || e.Status == filter.Status)
                      && (isSuper || e.CreatedBy == admin.Id
                          || (e.YearGroups != null && e.YearGroups.Any(__y => yearGroups.Contains(__y)))
                          || (e.CommunityId != null && communityIds.Contains(e.CommunityId))));

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

            var ticketedIds = result.Results.Where(e => e.IsTicketed).Select(e => e.Id).ToList();
            var sales = ticketedIds.Count == 0
                ? new Dictionary<string, (int Count, decimal Amount)>()
                : (await rsvpRepo.GetQueryable(r => ticketedIds.Contains(r.EventId) && r.Status == EventRsvpStatuses.Confirmed && r.AmountPaid > 0)
                        .GroupBy(r => r.EventId)
                        .Select(g => new { EventId = g.Key, Count = g.Count(), Amount = g.Sum(r => r.AmountPaid ?? 0m) })
                        .ToListAsync())
                    .ToDictionary(x => x.EventId, x => (x.Count, x.Amount));

            var dtoResult = new PgPagedResult<AlumniEventDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = result.Results.Select(e => WithSales(e.ToDto(), sales)).ToList(),
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

            if (!admin.CanViewScopedItem(ev.YearGroups, ev.CommunityId, ev.CreatedBy))
            {
                logger.LogWarning("Denied event view access for admin {AdminId} to event {EventId} (adminYear={AdminYear}, eventYears={EventYears}, createdBy={CreatedBy})",
                    admin.Id, eventId, admin.GraduationYear, ev.YearGroups ?? new List<int>(), ev.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<AlumniEventDto>("Event not found");
            }

            var dto = ev.ToDto();
            if (ev.IsTicketed)
            {
                var sold = await rsvpRepo.GetQueryable(r => r.EventId == eventId && r.Status == EventRsvpStatuses.Confirmed && r.AmountPaid > 0)
                    .GroupBy(r => r.EventId).Select(g => new { Count = g.Count(), Amount = g.Sum(r => r.AmountPaid ?? 0m) }).FirstOrDefaultAsync();
                dto.PaidTicketCount = sold?.Count ?? 0;
                dto.TicketAmountCollected = sold?.Amount ?? 0m;
            }
            return dto.ToOkApiResponse();
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
            var pricingError = await ValidatePricingAsync(request.IsTicketed, request.TicketPrice, null);
            if (pricingError is not null)
                return ApiResponseExtensions.ToBadRequestApiResponse<AlumniEventDto>(pricingError);
            if (CleanLocationLink(request.GoogleLocationUrl, out var locationLink) is { } linkError)
                return ApiResponseExtensions.ToBadRequestApiResponse<AlumniEventDto>(linkError);

            var resolvedCommunityId = admin.ResolveCommunityForCreation(request.CommunityId);
            var resolvedYearGroups = admin.ResolveYearGroupsForCreation(request.YearGroups);
            ScopeAuthorizationExtensions.NormalizeAudience(ref resolvedYearGroups, ref resolvedCommunityId);

            var descriptionHtml = RichText.Sanitize(request.Description);
            if (RichText.IsTooLong(descriptionHtml))
                return ApiResponseExtensions.ToBadRequestApiResponse<AlumniEventDto>(RichText.TooLongMessage);
            var ev = new AlumniEvent
            {
                CommunityId = resolvedCommunityId,
                Title = request.Title,
                Description = descriptionHtml,
                Venue = request.Venue,
                StartDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc),
                EndDate = request.EndDate.HasValue ? DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc) : (DateTime?)null,
                IsTicketed = request.IsTicketed,
                TicketPrice = request.IsTicketed ? request.TicketPrice : null,
                GoogleLocationUrl = locationLink,
                Capacity = request.Capacity,
                Status = "Upcoming",
                YearGroups = resolvedYearGroups,
                IsPublic = PublicVisibility.IsFullyPublic(request.IsPublic, resolvedCommunityId, resolvedYearGroups),
                YoutubeVideoUrls = request.YoutubeVideoUrls,
                CreatedBy = admin.Id,
            };

            // The banner and the gallery go to storage together rather than one after the other.
            Task<string>? bannerUpload = null;
            Task<List<string>>? galleryUpload = null;
            if (request.BannerImage is not null)
            {
                var name = $"{Guid.NewGuid():N}{Path.GetExtension(request.BannerImage.FileName)}";
                bannerUpload = storageService.UploadFileAsync(request.BannerImage, name, institutionSlug: currentTenant.InstitutionSlug ?? "");
            }
            if (request.Images is { Count: > 0 })
                galleryUpload = storageService.BulkUploadFilesAsync(request.Images, institutionSlug: currentTenant.InstitutionSlug ?? "");
            if (bannerUpload is not null) ev.BannerImageUrl = await bannerUpload;
            var galleryUrls = new List<string>();

            // A copy reuses the original's photos by address. Only addresses the original really has are taken, so the request
            // cannot be used to attach some other picture, and the original must be one this administrator may see.
            if (!string.IsNullOrWhiteSpace(request.CopyFromEventId))
            {
                var source = await eventRepo.GetByIdAsync(request.CopyFromEventId);
                if (source is null || !admin.CanViewScopedItem(source.YearGroups, source.CommunityId, source.CreatedBy))
                    return ApiResponseExtensions.ToNotFoundApiResponse<AlumniEventDto>("The event you are copying was not found");
                if (ev.BannerImageUrl is null && request.KeepBanner) ev.BannerImageUrl = source.BannerImageUrl;
                var original = source.ImageUrls ?? [];
                galleryUrls.AddRange((request.KeepImageUrls ?? []).Where(original.Contains).Distinct());
            }
            if (galleryUpload is not null) galleryUrls.AddRange(await galleryUpload);
            ev.ImageUrls = galleryUrls.Count > 0 ? galleryUrls : null;

            await eventRepo.AddAsync(ev);
            await InvalidatePublicEventsCacheAsync();
            logger.LogInformation("Event {EventId} created by admin {AdminId}", ev.Id, admin.Id);
            await temporalProvider.EnqueueNotificationAsync(NotificationRequest.EventReminder(currentTenant.InstitutionId!, ev.Id), logger);
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
            var wasCancelled = ev.Status == "Cancelled";
            var detailsChanged = ev.StartDate != DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc)
                || ev.EndDate != (request.EndDate.HasValue ? DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc) : null)
                || ev.Venue != request.Venue || ev.Capacity != request.Capacity;

            if (!admin.CanModifyScopedItem(ev.YearGroups, ev.CreatedBy, ev.CommunityId))
            {
                logger.LogWarning("Denied event update access for admin {AdminId} to event {EventId} (adminYear={AdminYear}, eventYears={EventYears}, createdBy={CreatedBy})",
                    admin.Id, request.EventId, admin.GraduationYear, ev.YearGroups ?? new List<int>(), ev.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<AlumniEventDto>("Event not found");
            }

            var pricingError = await ValidatePricingAsync(request.IsTicketed, request.TicketPrice, ev);
            if (pricingError is not null)
                return ApiResponseExtensions.ToBadRequestApiResponse<AlumniEventDto>(pricingError);
            if (CleanLocationLink(request.GoogleLocationUrl, out var locationLink) is { } linkError)
                return ApiResponseExtensions.ToBadRequestApiResponse<AlumniEventDto>(linkError);
            // A paid event stays on whatever it is once tickets exist, so it can't be cancelled by editing its status either.
            if (request.IsTicketed && request.Status == "Cancelled")
                return ApiResponseExtensions.ToBadRequestApiResponse<AlumniEventDto>(EventTicketRules.CannotCancelMessage);

            var updatedCommunityId = admin.ResolveCommunityForCreation(request.CommunityId);
            var updatedYearGroups = admin.ResolveYearGroupsForCreation(request.YearGroups);
            ScopeAuthorizationExtensions.NormalizeAudience(ref updatedYearGroups, ref updatedCommunityId);
            ev.CommunityId = updatedCommunityId;
            ev.Title = request.Title;
            var descriptionHtml = RichText.Sanitize(request.Description);
            if (RichText.IsTooLong(descriptionHtml))
                return ApiResponseExtensions.ToBadRequestApiResponse<AlumniEventDto>(RichText.TooLongMessage);
            ev.Description = descriptionHtml;
            ev.Venue = request.Venue;
            ev.StartDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc);
            ev.EndDate = request.EndDate.HasValue ? DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc) : (DateTime?)null;
            ev.IsTicketed = request.IsTicketed;
            ev.TicketPrice = request.IsTicketed ? request.TicketPrice : null;
            ev.GoogleLocationUrl = locationLink;
            ev.Capacity = request.Capacity;
            if (!string.IsNullOrWhiteSpace(request.Status)) ev.Status = request.Status;   // an edit that does not say keeps the current status
            ev.YearGroups = updatedYearGroups;
            ev.IsPublic = PublicVisibility.IsFullyPublic(request.IsPublic, updatedCommunityId, updatedYearGroups);
            ev.YoutubeVideoUrls = request.YoutubeVideoUrls;

            Task<string>? bannerUpload = null;
            Task<List<string>>? galleryUpload = null;
            if (request.BannerImage is not null)
            {
                var name = $"{Guid.NewGuid():N}{Path.GetExtension(request.BannerImage.FileName)}";
                bannerUpload = storageService.UploadFileAsync(request.BannerImage, name, institutionSlug: currentTenant.InstitutionSlug ?? "");
            }
            if (request.Images is { Count: > 0 })
                galleryUpload = storageService.BulkUploadFilesAsync(request.Images);
            if (bannerUpload is not null) ev.BannerImageUrl = await bannerUpload;

            var imageUrls = new List<string>();
            if (request.ExistingImageUrls is { Count: > 0 })
                imageUrls.AddRange(request.ExistingImageUrls);
            if (galleryUpload is not null)
                imageUrls.AddRange(await galleryUpload);
            ev.ImageUrls = imageUrls.Count > 0 ? imageUrls : null;

            ev.UpdatedAt = DateTime.UtcNow;
            ev.UpdatedBy = admin.Id;
            await eventRepo.UpdateAsync(ev);
            await InvalidatePublicEventsCacheAsync();
            // People who are going hear about a cancellation once (not every time the cancelled event is edited again), and about changed
            // details only while the event is still ahead. Fixing a past event's description or photos is housekeeping, not news.
            var nowCancelled = ev.Status == "Cancelled" && !wasCancelled;
            var stillAhead = ev.Status != "Cancelled" && ev.Status != "Completed" && (ev.EndDate ?? ev.StartDate) >= DateTime.UtcNow;
            if (nowCancelled || (detailsChanged && stillAhead))
            {
                var recipients = await rsvpRepo.GetAllAsync(r => r.EventId == ev.Id && r.Status == "Confirmed");
                foreach (var rsvp in recipients)
                    await temporalProvider.EnqueueNotificationAsync(
                        ev.Status == "Cancelled"
                            ? NotificationRequest.EventCancelled(ev.InstitutionId, rsvp.MemberId, ev.Id, ev.Title)
                            : NotificationRequest.EventDetailsChanged(ev.InstitutionId, rsvp.MemberId, ev.Id, ev.Title), logger);
            }
            logger.LogInformation("Event {EventId} updated by admin {AdminId}", ev.Id, admin.Id);
            return ev.ToDto().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating event {EventId} by admin {AdminId}", request.EventId, admin.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<AlumniEventDto>("Failed to update event");
        }
    }

    public async Task<IApiResponse<AlumniEventDto>> SetRegistrationClosedAsync(string eventId, bool closed, AuthData admin)
    {
        try
        {
            var ev = await eventRepo.GetByIdAsync(eventId);
            if (ev is null || !admin.CanModifyScopedItem(ev.YearGroups, ev.CreatedBy, ev.CommunityId))
                return ApiResponseExtensions.ToNotFoundApiResponse<AlumniEventDto>("Event not found");
            if (ev.Status is "Cancelled" or "Completed")
                return ApiResponseExtensions.ToBadRequestApiResponse<AlumniEventDto>("This event is already over, so there is nothing to open or close.");

            if (ev.RegistrationClosed != closed)
            {
                ev.RegistrationClosed = closed;
                ev.UpdatedAt = DateTime.UtcNow;
                ev.UpdatedBy = admin.Id;
                await eventRepo.UpdateAsync(ev);
                await InvalidatePublicEventsCacheAsync();
                await auditLog.LogAsync(admin, closed ? "Event RSVPs Closed" : "Event RSVPs Reopened", ev.Title);
            }
            var dto = ev.ToDto();
            dto.RsvpCount = await rsvpRepo.CountAsync(r => r.EventId == eventId && r.Status == EventRsvpStatuses.Confirmed);
            return dto.ToOkApiResponse(closed ? "RSVPs closed" : "RSVPs reopened");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error changing registration for event {EventId}", eventId);
            return ApiResponseExtensions.ToServerErrorApiResponse<AlumniEventDto>("Failed to change registration");
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

            if (!admin.CanModifyScopedItem(ev.YearGroups, ev.CreatedBy, ev.CommunityId))
            {
                logger.LogWarning("Denied event cancel access for admin {AdminId} to event {EventId} (adminYear={AdminYear}, eventYears={EventYears}, createdBy={CreatedBy})",
                    admin.Id, eventId, admin.GraduationYear, ev.YearGroups ?? new List<int>(), ev.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Event not found");
            }

            if (!EventTicketRules.CanCancel(ev))
                return ApiResponseExtensions.ToBadRequestApiResponse<object>(EventTicketRules.CannotCancelMessage);

            ev.Status = "Cancelled";
            ev.UpdatedAt = DateTime.UtcNow;
            ev.UpdatedBy = admin.Id;
            await eventRepo.UpdateAsync(ev);
            await InvalidatePublicEventsCacheAsync();
            var recipients = await rsvpRepo.GetAllAsync(r => r.EventId == ev.Id && r.Status == "Confirmed");
            foreach (var rsvp in recipients)
                await temporalProvider.EnqueueNotificationAsync(
                    NotificationRequest.EventCancelled(ev.InstitutionId, rsvp.MemberId, ev.Id, ev.Title), logger);
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

            if (!admin.CanModifyScopedItem(ev.YearGroups, ev.CreatedBy, ev.CommunityId))
            {
                logger.LogWarning("Denied event delete access for admin {AdminId} to event {EventId} (adminYear={AdminYear}, eventYears={EventYears}, createdBy={CreatedBy})",
                    admin.Id, eventId, admin.GraduationYear, ev.YearGroups ?? new List<int>(), ev.CreatedBy);
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Event not found");
            }

            if (ev.IsTicketed && await CountPaidTicketsAsync(ev.Id) > 0)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>(EventTicketRules.CannotDeleteMessage);

            foreach (var update in await updateRepo.GetAllAsync(u => u.EventId == eventId))
                await updateRepo.RemoveAsync(update);
            await eventRepo.RemoveAsync(ev);
            await InvalidatePublicEventsCacheAsync();
            await auditLog.LogAsync(admin, "Event Deleted", ev.Title);
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
            if (ev is null || !admin.CanViewScopedItem(ev.YearGroups, ev.CommunityId, ev.CreatedBy))
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

            // Backfill above only fills in RSVPs that never got a snapshot — this
            // refreshes name/photo on every row from the live Member records (one
            // batched lookup, not one query per row) so a later name/photo change
            // actually shows up, not just missing snapshots.
            var rsvpMemberIds = dtoResult.Results.Select(d => d.MemberId).Distinct().ToList();
            var rsvpMembers = (await memberRepo.GetAllAsync(m => rsvpMemberIds.Contains(m.Id))).ToDictionary(m => m.Id);
            foreach (var dto in dtoResult.Results)
            {
                if (rsvpMembers.TryGetValue(dto.MemberId, out var m))
                {
                    dto.MemberName = $"{m.FirstName} {m.LastName}";
                    dto.MemberEmail = m.Email;
                    dto.MemberProfilePictureUrl = m.ProfilePictureUrl;
                }
            }

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
            if (ev is null || !admin.CanModifyScopedItem(ev.YearGroups, ev.CreatedBy, ev.CommunityId))
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
            // A seat at a paid event is only ever given by a payment, never reopened by hand.
            if (ev.IsTicketed)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("Places at a paid event are only given by a payment.");

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
