using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// A graduating-class year group an institution admin has created (e.g. "Batch
/// of 2020"), replacing the old client-hardcoded 1952-to-current-year range on
/// the registration form. Deliberately not a foreign key target — Member.GraduationYear
/// stays a plain int matched by value, so removing a Batch never orphans a member record.
/// </summary>
public class Batch : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }
    public bool IsActive { get; set; } = true;
}
