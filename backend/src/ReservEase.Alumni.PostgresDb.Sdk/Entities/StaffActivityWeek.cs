namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// "This staff member used the institution portal during this week" — one row
/// per staff per ISO week (Monday 00:00 UTC), written by Institution.Api at
/// sign-in and on session refresh. The source of truth for the activation
/// rule "2+ staff active three weeks running": recorded where the activity
/// happens, so a worker outage can't leave holes in the history.
/// Platform-level (not ITenantScoped) — read cross-tenant by platform tooling.
/// </summary>
public class StaffActivityWeek : BaseEntity
{
    public string InstitutionId { get; set; } = string.Empty;
    public string StaffId { get; set; } = string.Empty;
    public DateTime WeekStart { get; set; }
}
