namespace ReservEase.Alumni.PostgresDb.Sdk.Services;

/// <summary>
/// Holds the resolved tenant (<see cref="Entities.Institution"/>) for the current request.
/// Set once, early in the pipeline, by each API's tenant-resolution middleware —
/// before any <see cref="DbContexts.AlumniDbContext"/> query runs. Registered scoped.
/// </summary>
public interface ICurrentTenantService
{
    string? InstitutionId { get; }

    /// <summary>The tenant's own slug — set alongside InstitutionId by TenantResolutionMiddleware (which already has the full Institution row on hand, so this costs no extra query). Used to file uploads under a per-institution subfolder; null wherever InstitutionId itself is unset (e.g. inside an actor's fresh scope, or in Platform.Api which has no tenant middleware at all).</summary>
    string? InstitutionSlug { get; }

    void SetInstitutionId(string institutionId, string? institutionSlug = null);
}

public class CurrentTenantService : ICurrentTenantService
{
    public string? InstitutionId { get; private set; }
    public string? InstitutionSlug { get; private set; }

    public void SetInstitutionId(string institutionId, string? institutionSlug = null)
    {
        InstitutionId = institutionId;
        InstitutionSlug = institutionSlug;
    }
}
