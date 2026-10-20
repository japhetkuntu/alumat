using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class EventUpdateService(
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<EventUpdate> updateRepo,
    IAlumniPgRepository<EventRsvp> rsvpRepo,
    IStorageService storageService,
    ITemporalClientProvider temporalProvider,
    ICurrentTenantService currentTenant,
    IInstitutionAuditLogService auditLog,
    ILogger<EventUpdateService> logger) : IEventUpdateService
{
    /// <summary>The event, if this admin may see it; null otherwise (shown as "not found" so scoped admins learn nothing).</summary>
    private async Task<AlumniEvent?> ViewableEventAsync(string eventId, AuthData admin)
    {
        var ev = await eventRepo.GetByIdAsync(eventId);
        return ev is not null && admin.CanViewScopedItem(ev.YearGroups, ev.CommunityId, ev.CreatedBy) ? ev : null;
    }

    private async Task<AlumniEvent?> ModifiableEventAsync(string eventId, AuthData admin)
    {
        var ev = await eventRepo.GetByIdAsync(eventId);
        return ev is not null && admin.CanModifyScopedItem(ev.YearGroups, ev.CreatedBy, ev.CommunityId) ? ev : null;
    }

    public async Task<IApiResponse<List<EventUpdateDto>>> ListAsync(string eventId, AuthData admin)
    {
        try
        {
            if (await ViewableEventAsync(eventId, admin) is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<List<EventUpdateDto>>("Event not found");
            var updates = await updateRepo.GetAllAsync(u => u.EventId == eventId);
            return EventUpdateRules.InDisplayOrder(updates)
                .Select(u => u.ToDto(includeAuthor: true)).ToList().ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error listing updates for event {EventId}", eventId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<EventUpdateDto>>("Failed to load updates");
        }
    }

    public Task<IApiResponse<EventUpdateDto>> CreateAsync(string eventId, EventUpdateSaveRequest request, IReadOnlyDictionary<string, List<IFormFile>> files, AuthData admin, string? copyFromUpdateId = null)
        => SaveAsync(eventId, null, request, files, admin, copyFromUpdateId);

    public Task<IApiResponse<EventUpdateDto>> UpdateAsync(string eventId, string updateId, EventUpdateSaveRequest request, IReadOnlyDictionary<string, List<IFormFile>> files, AuthData admin)
        => SaveAsync(eventId, updateId, request, files, admin);

    private async Task<IApiResponse<EventUpdateDto>> SaveAsync(string eventId, string? updateId, EventUpdateSaveRequest request, IReadOnlyDictionary<string, List<IFormFile>> files, AuthData admin, string? copyFromUpdateId = null)
    {
        try
        {
            var ev = await ModifiableEventAsync(eventId, admin);
            if (ev is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<EventUpdateDto>("Event not found");

            EventUpdate? existing = null;
            if (updateId is not null)
            {
                existing = await updateRepo.GetByIdAsync(updateId);
                if (existing is null || existing.EventId != eventId)
                    return ApiResponseExtensions.ToNotFoundApiResponse<EventUpdateDto>("Update not found");
            }

            var title = (request.Title ?? "").Trim();
            if (title.Length == 0) return Bad("Give the update a title.");
            if (title.Length > EventUpdateRules.MaxTitleLength) return Bad($"A title can be up to {EventUpdateRules.MaxTitleLength} characters.");
            var categoryError = EventUpdateRules.CleanCategory(request.Category, out var category);
            if (categoryError is not null) return Bad(categoryError);

            var blocks = request.Blocks ?? [];
            var blockById = blocks.Where(b => !string.IsNullOrWhiteSpace(b.Id)).GroupBy(b => b.Id).ToDictionary(g => g.Key, g => g.First());

            // Every upload must belong to a photos or files part of this update, be the right kind, and fit.
            long totalBytes = 0;
            foreach (var (blockId, list) in files)
            {
                if (!blockById.TryGetValue(blockId, out var block) || block.Type is not (EventUpdateBlockTypes.Images or EventUpdateBlockTypes.Files))
                    return Bad("A file was sent for a part that doesn't take files. Please try again.");
                foreach (var f in list)
                {
                    var uploadError = EventUpdateRules.ValidateUpload(block.Type, f.FileName, f.Length);
                    if (uploadError is not null) return Bad(uploadError);
                    totalBytes += f.Length;
                }
            }
            if (totalBytes > EventUpdateRules.MaxUploadBytes)
                return Bad($"That is more than {EventUpdateRules.MaxUploadBytes / 1024 / 1024} MB in one go. Save part of it now and add the rest in a second update.");

            var keepable = (existing?.Blocks ?? []).SelectMany(b => b.Items).Select(i => i.Url).ToHashSet();
            if (!string.IsNullOrWhiteSpace(copyFromUpdateId))
            {
                // A copy may keep the files of the update it was copied from, but only if the administrator may see that update's event.
                var source = await updateRepo.GetByIdAsync(copyFromUpdateId);
                if (source is null || await ViewableEventAsync(source.EventId, admin) is null)
                    return ApiResponseExtensions.ToNotFoundApiResponse<EventUpdateDto>("The update you are copying was not found");
                keepable.UnionWith(source.Blocks.SelectMany(b => b.Items).Select(i => i.Url));
            }
            var newCounts = files.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
            var blockError = EventUpdateRules.ValidateAndClean(blocks, keepable, newCounts);
            if (blockError is not null) return Bad(blockError);

            // Validation passed, so now it is safe to upload; a rejected form never leaves stray files behind.
            foreach (var block in blocks)
            {
                if (!files.TryGetValue(block.Id, out var uploads)) continue;
                var slug = currentTenant.InstitutionSlug ?? "";
                var uploaded = await Task.WhenAll(uploads.Select(async f =>
                {
                    var url = await storageService.UploadFileAsync(f, $"{Guid.NewGuid():N}{Path.GetExtension(f.FileName).ToLowerInvariant()}", institutionSlug: slug);
                    var name = Path.GetFileName(f.FileName);
                    return new EventUpdateItem { Url = url, Name = name.Length > 150 ? name[..150] : name, SizeBytes = f.Length, ContentType = f.ContentType };
                }));
                block.Items = [.. block.Items, .. uploaded];
            }

            EventUpdate update;
            if (existing is null)
            {
                update = new EventUpdate
                {
                    EventId = eventId, Title = title, Category = category, Blocks = blocks,
                    PostedByStaffId = admin.Id, PostedByName = $"{admin.FirstName} {admin.LastName}".Trim(),
                    CreatedBy = admin.Id,
                };
                await updateRepo.AddAsync(update);
                await auditLog.LogAsync(admin, "Event Update Posted", $"{ev.Title}: {title}");
                await NotifyAttendeesAsync(ev, update);
            }
            else
            {
                update = existing;
                update.Title = title;
                update.Category = category;
                update.Blocks = blocks;
                update.UpdatedAt = DateTime.UtcNow;
                update.UpdatedBy = admin.Id;
                await updateRepo.UpdateAsync(update);
                await auditLog.LogAsync(admin, "Event Update Edited", $"{ev.Title}: {title}");
            }

            return existing is null ? update.ToDto(true).ToCreatedApiResponse("Update posted") : update.ToDto(true).ToOkApiResponse("Update saved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error saving an update for event {EventId}", eventId);
            return ApiResponseExtensions.ToServerErrorApiResponse<EventUpdateDto>("Failed to save the update");
        }

        static IApiResponse<EventUpdateDto> Bad(string message) => ApiResponseExtensions.ToBadRequestApiResponse<EventUpdateDto>(message);
    }

    /// <summary>One in-app notice and push per member with a seat. A failure here never undoes the post.</summary>
    private async Task NotifyAttendeesAsync(AlumniEvent ev, EventUpdate update)
    {
        try
        {
            var attendees = await rsvpRepo.GetAllAsync(r => r.EventId == ev.Id && r.Status == EventRsvpStatuses.Confirmed);
            foreach (var rsvp in attendees)
                await temporalProvider.EnqueueNotificationAsync(
                    NotificationRequest.EventUpdatePosted(ev.InstitutionId, rsvp.MemberId, ev.Id, ev.Title, update.Title, update.Category), logger);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Posted update {UpdateId} but could not notify attendees of event {EventId}", update.Id, ev.Id);
        }
    }

    public async Task<IApiResponse<EventUpdateDto>> SetPinnedAsync(string eventId, string updateId, bool pinned, AuthData admin)
    {
        try
        {
            if (await ModifiableEventAsync(eventId, admin) is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<EventUpdateDto>("Event not found");
            var update = await updateRepo.GetByIdAsync(updateId);
            if (update is null || update.EventId != eventId)
                return ApiResponseExtensions.ToNotFoundApiResponse<EventUpdateDto>("Update not found");
            update.IsPinned = pinned;
            await updateRepo.UpdateAsync(update);
            return update.ToDto(true).ToOkApiResponse(pinned ? "Pinned" : "Unpinned");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error pinning update {UpdateId}", updateId);
            return ApiResponseExtensions.ToServerErrorApiResponse<EventUpdateDto>("Failed to change the update");
        }
    }

    public async Task<IApiResponse<List<EventUpdateDto>>> ReorderAsync(string eventId, IReadOnlyList<string> orderedIds, AuthData admin)
    {
        try
        {
            if (await ModifiableEventAsync(eventId, admin) is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<List<EventUpdateDto>>("Event not found");
            var updates = await updateRepo.GetAllAsync(u => u.EventId == eventId);
            var byId = updates.ToDictionary(u => u.Id);
            var ids = orderedIds.Distinct().ToList();
            if (ids.Any(id => !byId.ContainsKey(id)))
                return ApiResponseExtensions.ToBadRequestApiResponse<List<EventUpdateDto>>("One of those updates no longer exists. Reload and try again.");

            // 1..n in the order given. An update posted after the organiser opened the page is not in the list; it keeps 0 and stays on top.
            var changed = new List<EventUpdate>();
            for (var i = 0; i < ids.Count; i++)
            {
                var update = byId[ids[i]];
                if (update.SortOrder == i + 1) continue;
                update.SortOrder = i + 1;
                changed.Add(update);
            }
            if (changed.Count > 0) await updateRepo.UpdateRangeAsync(changed);
            return EventUpdateRules.InDisplayOrder(updates).Select(u => u.ToDto(includeAuthor: true)).ToList().ToOkApiResponse("Order saved");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error arranging updates for event {EventId}", eventId);
            return ApiResponseExtensions.ToServerErrorApiResponse<List<EventUpdateDto>>("Failed to save the order");
        }
    }

    public async Task<IApiResponse<object>> DeleteAsync(string eventId, string updateId, AuthData admin)
    {
        try
        {
            var ev = await ModifiableEventAsync(eventId, admin);
            if (ev is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Event not found");
            var update = await updateRepo.GetByIdAsync(updateId);
            if (update is null || update.EventId != eventId)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Update not found");
            await updateRepo.RemoveAsync(update);
            await auditLog.LogAsync(admin, "Event Update Deleted", $"{ev.Title}: {update.Title}");
            return new object().ToOkApiResponse("Update deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting update {UpdateId}", updateId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete the update");
        }
    }
}
