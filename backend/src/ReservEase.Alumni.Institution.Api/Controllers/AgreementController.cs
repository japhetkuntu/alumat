using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Institution.Api.Controllers;

public record AgreementStatusResponse(
    string CurrentVersion, bool Accepted, string? AcceptedVersion, DateTime? AcceptedAt, string? AcceptedByName, string? AcceptedByTitle,
    bool RequiresAcceptance);

public class AcceptAgreementRequest
{
    [Required] public string Version { get; set; } = string.Empty;
    /// <summary>The person's role at the institution, e.g. "Chairperson".</summary>
    [Required, MaxLength(120)] public string Title { get; set; } = string.Empty;
}

/// <summary>
/// The Institution Agreement, accepted electronically by the institution's Super Admin. Each acceptance is kept
/// as its own row, with who, when, from where, and which version.
/// </summary>
[Authorize]
public class AgreementController(
    IAlumniPgRepository<InstitutionAgreementAcceptance> acceptanceRepo,
    IInstitutionAuditLogService auditLog) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "Agreement status", Description = "Whether the current version of the Institution Agreement has been accepted. Only a Super Admin is asked to accept.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AgreementStatusResponse>))]
    public async Task<IActionResult> GetStatus()
    {
        var admin = User.GetAccount();
        var latest = (await acceptanceRepo.GetAllAsync()).OrderByDescending(a => a.AcceptedAt).FirstOrDefault();
        var accepted = latest is not null && latest.Version == InstitutionAgreement.CurrentVersion;
        var result = new AgreementStatusResponse(
            InstitutionAgreement.CurrentVersion, accepted, latest?.Version, latest?.AcceptedAt, latest?.AcceptedByName, latest?.AcceptedByTitle,
            RequiresAcceptance: !accepted && admin.Role == "SuperAdmin");
        return result.ToOkApiResponse().ToActionResult();
    }

    [HttpPost("accept")]
    [Authorize(Roles = "SuperAdmin")]
    [SwaggerOperation(Summary = "Accept the Institution Agreement")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AgreementStatusResponse>))]
    public async Task<IActionResult> Accept([FromBody] AcceptAgreementRequest request)
    {
        var admin = User.GetAccount();
        if (request.Version != InstitutionAgreement.CurrentVersion)
            return ApiResponseExtensions.ToBadRequestApiResponse<AgreementStatusResponse>("The agreement has been updated. Please reload and read the latest version.").ToActionResult();

        var forwarded = Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();
        var ip = string.IsNullOrEmpty(forwarded) ? HttpContext.Connection.RemoteIpAddress?.ToString() : forwarded;

        var acceptance = new InstitutionAgreementAcceptance
        {
            Version = request.Version,
            AcceptedByStaffId = admin.Id,
            AcceptedByName = admin.Name,
            AcceptedByEmail = admin.Email,
            AcceptedByTitle = request.Title.Trim(),
            AcceptedAt = DateTime.UtcNow,
            IpAddress = ip,
            CreatedBy = admin.Id,
        };
        await acceptanceRepo.AddAsync(acceptance);
        await auditLog.LogAsync(admin, "Institution Agreement Accepted", $"Version {request.Version}, as {acceptance.AcceptedByTitle}");

        var result = new AgreementStatusResponse(
            InstitutionAgreement.CurrentVersion, true, acceptance.Version, acceptance.AcceptedAt, acceptance.AcceptedByName, acceptance.AcceptedByTitle, RequiresAcceptance: false);
        return result.ToOkApiResponse().ToActionResult();
    }
}
