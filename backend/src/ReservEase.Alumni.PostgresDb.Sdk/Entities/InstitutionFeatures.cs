namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// Canonical keys for the product features platform staff can disable per
/// institution (see <see cref="Institution.DisabledFeatures"/>). Shared by
/// both Institution.Api and Member.Api so a feature disabled on one portal
/// is recognized identically on the other — e.g. disabling "Contributions"
/// blocks the institution staff's Campaigns/Contributions pages AND the
/// member's "My Contributions" page and API access, from the same key.
///
/// Deliberately excludes core account/admin plumbing every institution needs
/// regardless (dashboard, member roster/profile, notifications, institution
/// settings, staff management) — those are never gateable.
/// </summary>
public static class InstitutionFeatures
{
    public const string Contributions = "Contributions"; // campaigns, membership renewal, and contribution records
    public const string Events = "Events";
    public const string Jobs = "Jobs";
    public const string News = "News";
    public const string Forum = "Forum";
    public const string Mentorship = "Mentorship";
    public const string Resources = "Resources";
    public const string Spotlights = "Spotlights";
    public const string Leaderboard = "Leaderboard"; // member portal only
    public const string Referrals = "Referrals";     // member portal only
    public const string ClassNotes = "ClassNotes";   // member portal only
    public const string Directory = "Directory";     // member portal only
    public const string Communities = "Communities"; // member-created sub-communities with scoped forum/events/resources/campaigns
    /// <summary>Institution staff manually recording an offline/cash/bank-transfer payment — independent of online (Contributions) payments.</summary>
    public const string ManualPayments = "ManualPayments";

    public static readonly IReadOnlyList<string> All =
    [
        Contributions, Events, Jobs, News, Forum, Mentorship,
        Resources, Spotlights, Leaderboard, Referrals, ClassNotes, Directory,
        Communities, ManualPayments,
    ];

    /// <summary>
    /// Label + one-line description per key — the single source of truth the
    /// feature-catalog endpoint serves, so the toggle UI never has to hardcode
    /// its own copy of this list again (that drift is exactly how Communities
    /// shipped fully ungated the first time around).
    /// </summary>
    public static readonly IReadOnlyList<(string Key, string Label, string Description)> Catalog =
    [
        (Contributions, "Contributions", "Campaigns, membership renewal, and online payment collection."),
        (ManualPayments, "Manual payments", "Institution staff recording offline/cash/bank-transfer payments by hand."),
        (Events, "Events", "Alumni events with RSVP."),
        (Jobs, "Job board", "Alumni-posted job listings."),
        (News, "News", "Institution news and announcements."),
        (Forum, "Forum", "Open discussion threads."),
        (Mentorship, "Mentorship", "Mentor/mentee matching and requests."),
        (Resources, "Resources", "Shared files and links library."),
        (Spotlights, "Spotlights", "Featured alumni success stories."),
        (Leaderboard, "Leaderboard", "Class-year giving/engagement leaderboard."),
        (Referrals, "Referrals", "Member-to-member referral invites."),
        (ClassNotes, "Class notes", "The class-year social wall."),
        (Directory, "Alumni directory", "Searchable member directory."),
        (Communities, "Communities", "Member-created sub-communities with their own forum, events, resources, and campaigns."),
    ];
}
