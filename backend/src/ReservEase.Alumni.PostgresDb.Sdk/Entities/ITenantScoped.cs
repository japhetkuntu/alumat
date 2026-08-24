namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// Marks an entity as belonging to exactly one <see cref="Institution"/>.
/// <see cref="DbContexts.AlumniDbContext"/> applies a global query filter to every
/// entity implementing this interface, scoping all reads/writes to the request's
/// resolved tenant (see <see cref="Services.ICurrentTenantService"/>).
/// </summary>
public interface ITenantScoped
{
    string InstitutionId { get; set; }
}
