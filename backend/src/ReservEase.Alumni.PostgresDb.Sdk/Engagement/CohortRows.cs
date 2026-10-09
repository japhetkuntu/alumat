namespace ReservEase.Alumni.PostgresDb.Sdk.Engagement;

public record AmbassadorRef(string StaffId, string Name, bool Active);

/// <summary>One year group's reading. Counts only; no member is identifiable from it.</summary>
public record CohortRow(
    int Year, int ActiveMembers, int PendingMembers, int NewMembers, int Activated, int Participants,
    int InvitationsRegistered, int IncompleteProfiles, IReadOnlyList<AmbassadorRef> Ambassadors);

public record AmbassadorRow(
    string StaffId, string Name, IReadOnlyList<int> YearGroups, DateTime? LastActiveAt, int DaysSinceActive, bool Active,
    int TasksOpen, int TasksCompleted, int MembersInScope, int NewMembersInScope);
