using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.Platform.Api.Controllers;

/// <summary>
/// The single source of truth for what feature keys exist and what they mean —
/// backed by <see cref="InstitutionFeatures.Catalog"/>, so the toggle UI never
/// has to hardcode its own duplicate list again.
/// </summary>
[Authorize]
[Route("api/v{version:apiVersion}/features")]
public class FeaturesController : DefaultController
{
    [HttpGet("catalog")]
    [SwaggerOperation(Summary = "List all gateable feature keys with label + description")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<FeatureCatalogItem>>))]
    public IActionResult GetCatalog()
    {
        var items = InstitutionFeatures.Catalog
            .Select(f => new FeatureCatalogItem(f.Key, f.Label, f.Description))
            .ToList();
        return items.ToOkApiResponse().ToActionResult();
    }
}
