namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// Canonical values for <see cref="Institution.OrganizationType"/>. Open string
/// rather than a hard enum — extensible to a third type later with zero schema
/// change, matching the style of <see cref="Institution.Status"/>.
/// </summary>
/// <summary>
/// Whether a community uses graduation years at all. This is a choice the community makes about how it organises its members,
/// not a kind of organisation: Off for a club, a congregation or a professional body; Optional where some members have a year and
/// some do not; Required for an alumni-style community where the year is the spine. <see cref="Institution.OrganizationType"/> is
/// kept in step with it (Off is "Community", the others "Alumni") so everything built on that keeps working.
/// </summary>
public static class GraduationYearModes
{
    public const string Off = "Off";
    public const string Optional = "Optional";
    public const string Required = "Required";

    public static bool IsValid(string? mode) => mode is Off or Optional or Required;
    public static string OrganizationTypeFor(string mode) => mode == Off ? OrganizationTypes.Community : OrganizationTypes.Alumni;
}

public static class OrganizationTypes
{
    /// <summary>The platform's original shape: year-based cohorts via Batch/GraduationYear/YearGroups, "alumni" framing throughout.</summary>
    public const string Alumni = "Alumni";

    /// <summary>A non-alumni membership group with no graduation years — cohort/year UI is hidden, members self-organize via the existing Community/CommunityMembership system instead.</summary>
    public const string Community = "Community";
}
