using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>Lets a member report something another member posted. Institution admins review it.</summary>
[Authorize]
public class ReportsController(
    IAlumniPgRepository<ContentReport> reportRepo,
    IAlumniPgRepository<ForumThread> threadRepo,
    IAlumniPgRepository<MentorProfile> mentorRepo,
    IAlumniPgRepository<Job> jobRepo,
    IAlumniPgRepository<BusinessListing> listingRepo,
    IAlumniPgRepository<Spotlight> spotlightRepo,
    ILogger<ReportsController> logger) : DefaultController
{
    [HttpGet("reasons")]
    [SwaggerOperation(Summary = "List the reasons a member can pick when reporting")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<string[]>))]
    public IActionResult GetReasons() => ContentReportReasons.All.ToOkApiResponse().ToActionResult();

    [HttpPost]
    [SwaggerOperation(Summary = "Report content", Description = "Reports a forum thread, mentor profile, job, business listing or spotlight to the institution's admins.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> Create([FromBody] CreateContentReportRequest request)
    {
        var member = User.GetAccount();

        if (!ContentReportTypes.All.Contains(request.EntityType))
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("That kind of content can't be reported.").ToActionResult();
        if (!ContentReportReasons.All.Contains(request.Reason))
            return ApiResponseExtensions.ToBadRequestApiResponse<object>("Please choose a reason.").ToActionResult();

        var exists = request.EntityType switch
        {
            ContentReportTypes.ForumThread => await threadRepo.GetByIdAsync(request.EntityId) is not null,
            ContentReportTypes.MentorProfile => await mentorRepo.GetByIdAsync(request.EntityId) is not null,
            ContentReportTypes.Job => await jobRepo.GetByIdAsync(request.EntityId) is not null,
            ContentReportTypes.BusinessListing => await listingRepo.GetByIdAsync(request.EntityId) is not null,
            ContentReportTypes.Spotlight => await spotlightRepo.GetByIdAsync(request.EntityId) is not null,
            _ => false,
        };
        if (!exists)
            return ApiResponseExtensions.ToNotFoundApiResponse<object>("That item no longer exists.").ToActionResult();

        // Reporting the same thing twice while it is still open changes nothing, so it is accepted quietly.
        var already = await reportRepo.GetOneAsync(r =>
            r.ReporterMemberId == member.Id && r.EntityType == request.EntityType && r.EntityId == request.EntityId
            && r.Status == ContentReportStatuses.Open);
        if (already is null)
        {
            await reportRepo.AddAsync(new ContentReport
            {
                ReporterMemberId = member.Id,
                ReporterName = member.Name,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                EntityTitle = (request.EntityTitle ?? string.Empty).Trim(),
                Reason = request.Reason,
                Details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim(),
                CreatedBy = member.Id,
            });
            logger.LogInformation("Content report created by member {MemberId} for {Type} {EntityId}", member.Id, request.EntityType, request.EntityId);
        }

        return new object().ToOkApiResponse("Thank you. Your report has been sent to the administrators.").ToActionResult();
    }
}
