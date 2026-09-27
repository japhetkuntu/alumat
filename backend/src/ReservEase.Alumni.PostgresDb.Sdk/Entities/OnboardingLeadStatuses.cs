namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// The onboarding funnel, in order. Approved means the lead became a live
/// institution (see OnboardingLead.ApprovedInstitutionId); Rejected sits
/// outside the ordering.
/// </summary>
public static class OnboardingLeadStatuses
{
    public const string New = "New";
    public const string Contacted = "Contacted";
    public const string DemoBooked = "DemoBooked";
    public const string Trial = "Trial";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static readonly IReadOnlyList<string> All = [New, Contacted, DemoBooked, Trial, Approved, Rejected];

    /// <summary>
    /// Stamps the first-reached timestamp for <paramref name="status"/> and any
    /// earlier stage the lead skipped. Existing timestamps are never moved.
    /// </summary>
    public static void StampStage(OnboardingLead lead, string status, DateTime now)
    {
        var rank = status switch
        {
            Contacted => 1,
            DemoBooked => 2,
            Trial => 3,
            Approved => 4,
            _ => 0,
        };
        if (rank >= 1) lead.ContactedAt ??= now;
        if (rank >= 2) lead.DemoBookedAt ??= now;
        if (rank >= 3) lead.TrialStartedAt ??= now;
        if (rank >= 4) lead.ApprovedAt ??= now;
    }
}
