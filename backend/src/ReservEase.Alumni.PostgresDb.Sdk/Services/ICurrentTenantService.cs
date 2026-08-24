namespace ReservEase.Alumni.PostgresDb.Sdk.Services;

/// <summary>
/// Holds the resolved tenant (<see cref="Entities.Institution"/>) for the current request.
/// Set once, early in the pipeline, by each API's tenant-resolution middleware —
/// before any <see cref="DbContexts.AlumniDbContext"/> query runs. Registered scoped.
/// </summary>
public interface ICurrentTenantService
{
    string? InstitutionId { get; }

    void SetInstitutionId(string institutionId);
}

public class CurrentTenantService : ICurrentTenantService
{
    public string? InstitutionId { get; private set; }

    public void SetInstitutionId(string institutionId) => InstitutionId = institutionId;
}
