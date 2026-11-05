using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>How much members are using introductions. Counts only: an admin never sees who asked whom, a note, or a message.</summary>
[Authorize(Roles = "SuperAdmin")]
public class IntroductionsController(
    IAlumniPgRepository<Introduction> introRepo,
    IAlumniPgRepository<ContentReport> reportRepo) : DefaultController
{
    public record IntroductionCountsResponse(int RequestsLast30Days, int AcceptedLast30Days, int ActiveConversations, int OpenReports);

    [HttpGet("counts")]
    [SwaggerOperation(Summary = "Introduction counts for the last 30 days")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IntroductionCountsResponse>))]
    public async Task<IActionResult> Counts()
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var requests = await introRepo.CountAsync(i => i.CreatedAt > since);
        var accepted = await introRepo.CountAsync(i => i.RespondedAt > since && i.Status != IntroductionStatuses.Declined && i.Status != IntroductionStatuses.Pending && i.Status != IntroductionStatuses.Withdrawn);
        var active = await introRepo.CountAsync(i => i.Status == IntroductionStatuses.Accepted && i.LastMessageAt > since);
        var reports = await reportRepo.CountAsync(r => r.EntityType == ContentReportTypes.Introduction && r.Status == ContentReportStatuses.Open);
        return new IntroductionCountsResponse(requests, accepted, active, reports).ToOkApiResponse().ToActionResult();
    }
}
