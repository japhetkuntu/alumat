namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// One row per institution per ISO week (Monday 00:00 UTC), written by the
/// Operations Worker's daily InstitutionActivationDispatchWorkflow. Exists
/// because InstitutionStaff.LastLoginAt / Member.LastLoginAt only hold the
/// most recent login — "active three weeks in a row" needs history.
/// The current week's row is upserted on every run, so a missed day never
/// loses a week: LastLoginAt >= WeekStart is monotonic within the week.
/// Platform-level (not ITenantScoped) — only ever read cross-tenant by
/// platform staff and the worker.
/// </summary>
public class InstitutionActivitySnapshot : BaseEntity
{
    public string InstitutionId { get; set; } = string.Empty;
    /// <summary>Monday 00:00 UTC of the week this row covers — see InstitutionActivationCalculator.WeekStart.</summary>
    public DateTime WeekStart { get; set; }
    /// <summary>Non-disabled institution staff whose LastLoginAt fell within this week.</summary>
    public int ActiveStaffCount { get; set; }
    /// <summary>Approved (Status "Active") members at the time of the latest run this week.</summary>
    public int MemberCount { get; set; }
    /// <summary>Approved members who have ever logged in.</summary>
    public int MembersEverLoggedIn { get; set; }
    /// <summary>Approved members whose LastLoginAt fell within this week.</summary>
    public int MembersActiveThisWeek { get; set; }
    public int SuccessfulPaymentsThisWeek { get; set; }
    public decimal AmountCollectedThisWeek { get; set; }
}
