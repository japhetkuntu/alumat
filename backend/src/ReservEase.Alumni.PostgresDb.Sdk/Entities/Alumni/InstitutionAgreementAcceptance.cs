namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// A record that an institution's Super Admin accepted a version of the Institution Agreement on its behalf.
/// Rows are never edited: a new version means a new row, so the full history stays.
/// </summary>
public class InstitutionAgreementAcceptance : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;
    public string AcceptedByStaffId { get; set; } = string.Empty;
    public string AcceptedByName { get; set; } = string.Empty;
    public string AcceptedByEmail { get; set; } = string.Empty;
    /// <summary>The role the person gave, e.g. "Chairperson". Free text, as typed.</summary>
    public string AcceptedByTitle { get; set; } = string.Empty;
    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
}
