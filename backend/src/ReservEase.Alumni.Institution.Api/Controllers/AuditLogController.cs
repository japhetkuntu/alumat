using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Controllers;

/// <summary>SuperAdmin-only accountability trail for this institution's own admins — an institution can have several, so this is how one SuperAdmin sees what every other admin has done.</summary>
[Authorize(Roles = "SuperAdmin")]
[Route("api/v{version:apiVersion}/audit-log")]
public class AuditLogController(IInstitutionAuditLogService auditLogService) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "This institution's admin audit trail")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PgPagedResult<InstitutionAuditLogEntryResponse>>))]
    public async Task<IActionResult> GetEntries([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null)
    {
        var result = await auditLogService.GetEntriesAsync(page, pageSize, search);
        return result.ToActionResult();
    }
}
