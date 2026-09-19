namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// Canonical values for <see cref="Institution.OrganizationType"/>. Open string
/// rather than a hard enum — extensible to a third type later with zero schema
/// change, matching the style of <see cref="Institution.Status"/>.
/// </summary>
public static class OrganizationTypes
{
    /// <summary>The platform's original shape: year-based cohorts via Batch/GraduationYear/YearGroups, "alumni" framing throughout.</summary>
    public const string Alumni = "Alumni";

    /// <summary>A non-alumni membership group with no graduation years — cohort/year UI is hidden, members self-organize via the existing Community/CommunityMembership system instead.</summary>
    public const string Community = "Community";
}
