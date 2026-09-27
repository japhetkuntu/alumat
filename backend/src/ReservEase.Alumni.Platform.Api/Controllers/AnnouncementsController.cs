using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;

namespace ReservEase.Alumni.Platform.Api.Controllers;

[Authorize]
[Route("api/v{version:apiVersion}/announcements")]
public class AnnouncementsController(IAnnouncementService announcementService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List sent announcements")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<AnnouncementResponse>>))]
    public async Task<IActionResult> GetAnnouncements()
    {
        var result = await announcementService.GetAnnouncementsAsync();
        return result.ToActionResult();
    }

    [Authorize(Roles = "SuperAdmin,Support")]
    [HttpPost]
    [SwaggerOperation(Summary = "Send an announcement to institution admins")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<AnnouncementResponse>))]
    public async Task<IActionResult> Send([FromBody] SendAnnouncementRequest request)
    {
        var acct = User.GetAccount();
        var result = await announcementService.SendAsync(request, acct.Id, $"{acct.FirstName} {acct.LastName}".Trim());
        return result.ToActionResult();
    }

    [HttpGet("staff-directory")]
    [SwaggerOperation(Summary = "Search institution admins", Description = "For picking specific recipients — searches by name or email across every institution, or within one when institutionId is given.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<StaffDirectoryEntry>>))]
    public async Task<IActionResult> SearchStaff([FromQuery] string? search, [FromQuery] string? institutionId)
    {
        var result = await announcementService.SearchStaffAsync(search, institutionId);
        return result.ToActionResult();
    }
}
