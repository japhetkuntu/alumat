using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>Read-only list of this institution's active batches, for the registration form's graduation-year dropdown.</summary>
[AllowAnonymous]
public class BatchesController(IAlumniPgRepository<Batch> batchRepo) : DefaultController
{
    [HttpGet]
    [SwaggerOperation(Summary = "List active batches", Description = "Get this institution's active batches for the registration form")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBatches()
    {
        var batches = await batchRepo.GetAllAsync(b => b.IsActive);
        var items = batches
            .OrderByDescending(b => b.Year)
            .Select(b => new BatchDto(b.Id, b.Name, b.Year));
        return new OkObjectResult(new ApiResponse<IEnumerable<BatchDto>>
        {
            Code = 200, SubCode = "OK", Message = "Batches retrieved",
            Data = items,
        });
    }
}

public record BatchDto(string Id, string Name, int Year);
