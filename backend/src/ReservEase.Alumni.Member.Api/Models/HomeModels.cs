namespace ReservEase.Alumni.Member.Api.Models;

/// <summary>What a person did, as the home feed tells it. The member portal turns each kind into a sentence and a link.</summary>
public static class HomeFeedKinds
{
    public const string MemberJoined = "MemberJoined";
    public const string Spotlight = "Spotlight";
    public const string Birthday = "Birthday";
    public const string ForumThread = "ForumThread";
    public const string MentorJoined = "MentorJoined";
    public const string BusinessListed = "BusinessListed";
}

public class HomeFeedItemDto
{
    public string Kind { get; set; } = string.Empty;
    /// <summary>The thing to open — a forum thread, a business listing. Null where the kind links to a list page.</summary>
    public string? EntityId { get; set; }
    public string PersonId { get; set; } = string.Empty;
    public string PersonName { get; set; } = string.Empty;
    public string? PersonPhotoUrl { get; set; }
    public int? PersonGraduationYear { get; set; }
    public bool SameYearGroup { get; set; }
    public bool SameDepartment { get; set; }
    /// <summary>The thread title, business name, mentorship area or spotlight headline. Null when the sentence says it all (someone joined).</summary>
    public string? Title { get; set; }
    public DateTime OccurredAt { get; set; }
}

public class HomeFeedDto
{
    /// <summary>When the member last opened home, before this visit. Null on their first visit.</summary>
    public DateTime? LastSeenAt { get; set; }
    public List<HomeFeedItemDto> Items { get; set; } = [];
}

public class HomeModulesDto
{
    /// <summary>Feature keys (see InstitutionFeatures) that are switched on but have nothing in them for this member yet.</summary>
    public List<string> Empty { get; set; } = [];
    /// <summary>Whether the member has ever placed a store order — their orders page stays reachable even after the store itself empties out.</summary>
    public bool HasStoreOrders { get; set; }
}
