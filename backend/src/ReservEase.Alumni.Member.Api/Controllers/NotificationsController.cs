using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Redis.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Controllers;

[Authorize]
public class NotificationsController(
    IAlumniPgRepository<Notification> notifRepo,
    IRedisService<MemberRedisConfig> cache) : DefaultController
{
    /// <summary>
    /// The unread-count badge polls every 30s from every open tab (see
    /// member-layout.tsx) — a short cache here means N tabs for the same
    /// member share one DB hit per window instead of one each, and the two
    /// mutations below evict it immediately so marking something read never
    /// feels laggy on the same device that just did it.
    /// </summary>
    private static string UnreadCountCacheKey(string memberId) => $"notif-unread-count:{memberId}";
    private static string UnreadByCategoryCacheKey(string memberId) => $"notif-unread-category:{memberId}";

    /// <summary>Maps a notification Type to the nav section its badge belongs on (see Notification.cs for the full Type list).</summary>
    private static readonly Dictionary<string, string> TypeToCategory = new()
    {
        ["JobAlert"] = "jobs",
        ["EventReminder"] = "events",
        ["ForumReply"] = "forum",
    };

    [HttpGet]
    [SwaggerOperation(Summary = "Get notifications", Description = "Get paginated in-app notifications for the current member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<NotificationDto>>))]
    public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var member = User.GetAccount();
        var result = await notifRepo.GetPagedAsync(
            page, pageSize, "CreatedAt", "desc",
            n => n.RecipientId == member.Id && n.RecipientType == "Member");

        var paged = new PgPagedResult<NotificationDto>
        {
            PageIndex = result.PageIndex,
            PageSize = result.PageSize,
            Count = result.Count,
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages,
            LowerBoundSize = result.LowerBoundSize,
            UpperBoundSize = result.UpperBoundSize,
            Results = result.Results.Select(n => n.ToDto()).ToList(),
        };
        return paged.ToOkApiResponse().ToActionResult();
    }

    [HttpGet("unread-count")]
    [SwaggerOperation(Summary = "Unread count", Description = "Get the number of unread notifications")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<int>))]
    public async Task<IActionResult> GetUnreadCount()
    {
        var member = User.GetAccount();
        var cacheKey = UnreadCountCacheKey(member.Id);
        var cached = await cache.GetAsync<int?>(cacheKey);
        if (cached is { } count) return count.ToOkApiResponse().ToActionResult();

        var fresh = await notifRepo.CountAsync(n => n.RecipientId == member.Id && n.RecipientType == "Member" && !n.IsRead);
        await cache.SetAsync(cacheKey, fresh, TimeSpan.FromSeconds(20));
        return fresh.ToOkApiResponse().ToActionResult();
    }

    [HttpGet("unread-by-category")]
    [SwaggerOperation(Summary = "Unread count by category", Description = "Unread notification counts bucketed by nav section (jobs/events/forum), for bottom-nav badges")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<Dictionary<string, int>>))]
    public async Task<IActionResult> GetUnreadByCategory()
    {
        var member = User.GetAccount();
        var cacheKey = UnreadByCategoryCacheKey(member.Id);
        var cached = await cache.GetAsync<Dictionary<string, int>>(cacheKey);
        if (cached is not null) return cached.ToOkApiResponse().ToActionResult();

        var unread = await notifRepo.GetAllAsync(n => n.RecipientId == member.Id && n.RecipientType == "Member" && !n.IsRead);
        var result = new Dictionary<string, int> { ["jobs"] = 0, ["events"] = 0, ["forum"] = 0 };
        foreach (var n in unread)
        {
            if (TypeToCategory.TryGetValue(n.Type, out var category))
                result[category]++;
        }
        await cache.SetAsync(cacheKey, result, TimeSpan.FromSeconds(20));
        return result.ToOkApiResponse().ToActionResult();
    }

    [HttpPut("{id}/read")]
    [SwaggerOperation(Summary = "Mark as read", Description = "Mark a specific notification as read")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> MarkRead(string id)
    {
        var member = User.GetAccount();
        var notif = await notifRepo.GetByIdAsync(id);
        if (notif is null || notif.RecipientId != member.Id)
            return ApiResponseExtensions.ToNotFoundApiResponse<object>("Notification not found").ToActionResult();

        if (!notif.IsRead)
        {
            notif.IsRead = true;
            notif.ReadAt = DateTime.UtcNow;
            await notifRepo.UpdateAsync(notif);
            await cache.RemoveAsync(UnreadCountCacheKey(member.Id));
            await cache.RemoveAsync(UnreadByCategoryCacheKey(member.Id));
        }
        return new object().ToOkApiResponse().ToActionResult();
    }

    [HttpPut("read-all")]
    [SwaggerOperation(Summary = "Mark all as read", Description = "Mark all notifications as read for the current member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> MarkAllRead()
    {
        var member = User.GetAccount();
        var unread = (await notifRepo.GetAllAsync(
            n => n.RecipientId == member.Id && n.RecipientType == "Member" && !n.IsRead)).ToList();

        if (unread.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var n in unread)
            {
                n.IsRead = true;
                n.ReadAt = now;
            }
            await notifRepo.UpdateRangeAsync(unread);
            await cache.RemoveAsync(UnreadCountCacheKey(member.Id));
            await cache.RemoveAsync(UnreadByCategoryCacheKey(member.Id));
        }
        return new object().ToOkApiResponse("All notifications marked as read").ToActionResult();
    }
}
