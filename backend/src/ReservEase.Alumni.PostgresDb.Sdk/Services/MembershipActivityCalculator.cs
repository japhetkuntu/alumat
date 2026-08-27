namespace ReservEase.Alumni.PostgresDb.Sdk.Services;

/// <summary>
/// Resolves the final "is this member active" answer from an institution's
/// <see cref="Entities.Institution.MemberActivePolicy"/> and a caller-computed
/// dues-requirement signal. Each call site keeps its own way of computing
/// "has dues been paid" (write paths and read paths have historically used
/// slightly different definitions — e.g. all-years-required vs current-year-only
/// for display) — this only decides whether that signal matters at all under
/// the institution's chosen policy.
/// </summary>
public static class MembershipActivityCalculator
{
    public const string DuesRequiredPolicy = "DuesRequired";
    public const string ApprovedOnlyPolicy = "ApprovedOnly";

    /// <summary>
    /// Under "ApprovedOnly", dues are irrelevant — active iff the member's
    /// approval status is "Active". Under "DuesRequired" (default), defers
    /// entirely to the caller's own dues-paid computation.
    /// </summary>
    public static bool ResolveActive(string? policy, string? memberStatus, bool duesRequirementMet) =>
        policy == ApprovedOnlyPolicy ? memberStatus == "Active" : duesRequirementMet;
}
