namespace ReservEase.Alumni.PostgresDb.Sdk.Engagement;

/// <summary>
/// What counts as a meaningful action, in one place. Engagement is read from the records the product already keeps
/// (each of these has an author or member and a timestamp), not from a second event log: a parallel log would be a second
/// source of truth to keep in step, and would miss everything that happened before it existed. Page loads and sign-ins are
/// deliberately not here; a sign-in shows presence (it feeds "active members") but not participation.
/// </summary>
public static class EngagementTaxonomy
{
    public const string ForumThreadStarted = "ForumThreadStarted";
    public const string ForumReplyPosted = "ForumReplyPosted";
    public const string EventRsvpConfirmed = "EventRsvpConfirmed";
    public const string ContributionCompleted = "ContributionCompleted";
    public const string ClassNotePosted = "ClassNotePosted";

    /// <summary>The meaningful member actions, in the order the dashboard describes them.</summary>
    public static readonly IReadOnlyList<string> MeaningfulActions =
    [
        ForumThreadStarted, ForumReplyPosted, EventRsvpConfirmed, ContributionCompleted, ClassNotePosted,
    ];
}
