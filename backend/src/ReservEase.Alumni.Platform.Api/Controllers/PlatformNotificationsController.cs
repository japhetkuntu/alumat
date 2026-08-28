using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>In-app notifications for the current platform staff member — currently only raised when an institution opens a support ticket.</summary>
[Authorize]
[Route("api/v{version:apiVersion}/notifications")]
public class PlatformNotificationsController(IAlumniPgRepository<PlatformNotification> notifRepo) : DefaultController
{
    private static PlatformNotificationDto ToDto(PlatformNotification n) => new(
        n.Id, n.Title, n.Body, n.Type, n.IsRead, n.ReadAt, n.RelatedEntityId, n.RelatedEntityType, n.ActionUrl, n.CreatedAt);

    [HttpGet]
    [SwaggerOperation(Summary = "Get notifications", Description = "Paginated in-app notifications for the current platform staff member")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<PlatformNotificationDto>>))]
    public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var staff = User.GetAccount();
        var result = await notifRepo.GetPagedAsync(page, pageSize, "CreatedAt", "desc", n => n.RecipientStaffId == staff.Id);

        var paged = new PgPagedResult<PlatformNotificationDto>
        {
            PageIndex = result.PageIndex,
            PageSize = result.PageSize,
            Count = result.Count,
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages,
            LowerBoundSize = result.LowerBoundSize,
            UpperBoundSize = result.UpperBoundSize,
            Results = result.Results.Select(ToDto).ToList(),
        };
        return paged.ToOkApiResponse().ToActionResult();
    }

    [HttpGet("unread-count")]
    [SwaggerOperation(Summary = "Unread count")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<int>))]
    public async Task<IActionResult> GetUnreadCount()
    {
        var staff = User.GetAccount();
        var all = await notifRepo.GetAllAsync(n => n.RecipientStaffId == staff.Id && !n.IsRead);
        return all.Count().ToOkApiResponse().ToActionResult();
    }

    [HttpPut("{id}/read")]
    [SwaggerOperation(Summary = "Mark as read")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> MarkRead(string id)
    {
        var staff = User.GetAccount();
        var notif = await notifRepo.GetByIdAsync(id);
        if (notif is null || notif.RecipientStaffId != staff.Id)
            return ApiResponseExtensions.ToNotFoundApiResponse<object>("Notification not found").ToActionResult();

        if (!notif.IsRead)
        {
            notif.IsRead = true;
            notif.ReadAt = DateTime.UtcNow;
            await notifRepo.UpdateAsync(notif);
        }
        return new object().ToOkApiResponse().ToActionResult();
    }

    [HttpPut("read-all")]
    [SwaggerOperation(Summary = "Mark all as read")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> MarkAllRead()
    {
        var staff = User.GetAccount();
        var unread = (await notifRepo.GetAllAsync(n => n.RecipientStaffId == staff.Id && !n.IsRead)).ToList();

        if (unread.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var n in unread)
            {
                n.IsRead = true;
                n.ReadAt = now;
            }
            await notifRepo.UpdateRangeAsync(unread);
        }
        return new object().ToOkApiResponse("All notifications marked as read").ToActionResult();
    }
}
