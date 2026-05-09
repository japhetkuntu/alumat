using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Umat.Alumni.Common.Sdk.Extensions;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Extensions;
using Umat.Alumni.PostgresDb.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Repositories;

namespace Umat.Alumni.Admin.Api.Controllers;

[Authorize]
public class NotificationsController(
    IAlumniPgRepository<Notification> notifRepo) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Get notifications", Description = "Get paginated in-app notifications for the current admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<NotificationDto>>))]
    public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var admin = User.GetAccount();
        var result = await notifRepo.GetPagedAsync(
            page, pageSize, "CreatedAt", "desc",
            n => n.RecipientId == admin.Id && n.RecipientType == "Admin");

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
    [SwaggerOperation(Summary = "Unread count", Description = "Get the number of unread notifications for the current admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<int>))]
    public async Task<IActionResult> GetUnreadCount()
    {
        var admin = User.GetAccount();
        var all = await notifRepo.GetAllAsync(
            n => n.RecipientId == admin.Id && n.RecipientType == "Admin" && !n.IsRead);
        return all.Count().ToOkApiResponse().ToActionResult();
    }

    [HttpPut("{id}/read")]
    [SwaggerOperation(Summary = "Mark as read", Description = "Mark a specific notification as read")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> MarkRead(string id)
    {
        var admin = User.GetAccount();
        var notif = await notifRepo.GetByIdAsync(id);
        if (notif is null || notif.RecipientId != admin.Id)
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
    [SwaggerOperation(Summary = "Mark all as read", Description = "Mark all notifications as read for the current admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> MarkAllRead()
    {
        var admin = User.GetAccount();
        var unread = (await notifRepo.GetAllAsync(
            n => n.RecipientId == admin.Id && n.RecipientType == "Admin" && !n.IsRead)).ToList();

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
