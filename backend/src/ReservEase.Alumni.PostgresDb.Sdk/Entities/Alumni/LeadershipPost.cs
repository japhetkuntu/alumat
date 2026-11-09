namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// What a role can be allowed to do. This is a list of actions the platform knows how to perform and check, not a list of roles: the
/// institution makes its own roles with its own names and ticks which of these each one may do. Anything that touches where money
/// goes, institution settings, staff accounts or the roles themselves is deliberately absent, so no role can grant itself more.
/// </summary>
/// <summary>The limits on how often a leader may message their group.</summary>
public static class GroupMessaging
{
    public const int DefaultPerDay = 3;
    public const int MaxPerDay = 10;
    /// <summary>A stored value outside the allowed range, such as none at all, counts as the default.</summary>
    public static int Limit(int? stored) => stored is >= 1 ? Math.Min(stored.Value, MaxPerDay) : DefaultPerDay;
}

public static class LeadershipPrivileges
{
    public const string ManageMembers = "ManageMembers";
    public const string MessageGroup = "MessageGroup";
    public const string ManageEvents = "ManageEvents";
    public const string PostNews = "PostNews";
    public const string PostJobs = "PostJobs";
    public const string ShareResources = "ShareResources";
    public const string ManageFundraisers = "ManageFundraisers";
    public const string ViewGiving = "ViewGiving";
    public const string ViewReport = "ViewReport";

    public sealed record Info(string Key, string Label, string Description);

    /// <summary>Every privilege, in the order they are offered.</summary>
    public static readonly IReadOnlyList<Info> All =
    [
        new(ManageMembers, "Welcome and approve members", "See who has joined or asked to join, and approve the requests. Requests to join a group can also be declined."),
        new(MessageGroup, "Message the whole group", "Send a message to everyone in the group, as a notification and, where your community allows, by email or text."),
        new(ManageEvents, "Create and manage events", "Create free events for the group, and edit or cancel the ones they made."),
        new(PostNews, "Post news", "Write news and announcements for the group, and edit or delete their own."),
        new(PostJobs, "Post jobs and opportunities", "Share a job or opportunity with the group, and edit or close their own."),
        new(ShareResources, "Share resources", "Share useful links with the group, and edit or remove their own."),
        new(ManageFundraisers, "Run fundraisers", "Start fundraisers for the group, and edit or close the ones they made. Money always goes to the account your administrators set up."),
        new(ViewGiving, "See the group's giving", "See how much each of the group's fundraisers has raised, and who has given."),
        new(ViewReport, "See the group's report", "See how many members the group has, who is active, and how complete their profiles are."),
    ];

    public static bool IsValid(string? key) => All.Any(p => p.Key == key);
}

/// <summary>
/// A role an institution has made for its leaders ("Treasurer", "Chapter Convener"): its own name and the privileges it carries. Changing
/// a role changes it for everyone who holds it.
/// </summary>
public class LeadershipRole : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Keys from <see cref="LeadershipPrivileges"/>. Empty means recognition only.</summary>
    public List<string> Privileges { get; set; } = [];
}

/// <summary>
/// A member holding a role for a year group, a community or the whole institution. The role's privileges apply inside that group
/// only; a post for the whole institution applies everywhere. Removing a post deletes the row; the audit log keeps who held it.
/// </summary>
public class LeadershipPost : BaseEntity, ITenantScoped
{
    public const string ScopeInstitution = "Institution";
    public const string ScopeBatch = "Batch";
    public const string ScopeCommunity = "Community";

    public string InstitutionId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    /// <summary>Institution, Batch (a year group) or Community.</summary>
    public string ScopeType { get; set; } = ScopeInstitution;
    /// <summary>The year group's or community's id. Empty for the whole institution.</summary>
    public string ScopeId { get; set; } = string.Empty;

    public static bool IsValidScope(string? scopeType) => scopeType is ScopeInstitution or ScopeBatch or ScopeCommunity;
}
