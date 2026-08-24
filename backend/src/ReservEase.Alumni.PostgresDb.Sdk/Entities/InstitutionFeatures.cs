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

    public static readonly IReadOnlyList<string> All =
    [
        Contributions, Events, Jobs, News, Forum, Mentorship,
        Resources, Spotlights, Leaderboard, Referrals, ClassNotes, Directory,
    ];
}
