using ReservEase.Alumni.Platform.Api.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

/// <summary>
/// The reporting structure of the platform team, as pure rules over a list of staff, so it can be tested without a database.
/// Platform staff are few, so callers load them all and ask questions of the result.
/// </summary>
public class StaffHierarchy(IReadOnlyCollection<PlatformStaff> staff)
{
    /// <summary>A reporting chain longer than this is almost certainly a mistake, and keeps every walk bounded.</summary>
    public const int MaxDepth = 8;

    private readonly Dictionary<string, PlatformStaff> byId = staff.ToDictionary(s => s.Id);
    private readonly ILookup<string, PlatformStaff> reports = staff.Where(s => s.ReportsToStaffId is not null).ToLookup(s => s.ReportsToStaffId!);

    /// <summary>The person themselves and everyone who reports to them, directly or through others.</summary>
    public HashSet<string> SelfAndDescendants(string staffId)
    {
        var result = new HashSet<string> { staffId };
        var queue = new Queue<string>([staffId]);
        while (queue.Count > 0)
        {
            foreach (var r in reports[queue.Dequeue()])
                if (result.Add(r.Id)) queue.Enqueue(r.Id);   // the Add guard also makes a bad cycle in old data harmless
        }
        return result;
    }

    /// <summary>The chain above someone, nearest manager first.</summary>
    public List<PlatformStaff> ManagersOf(string staffId)
    {
        var chain = new List<PlatformStaff>();
        var seen = new HashSet<string> { staffId };
        var current = byId.GetValueOrDefault(staffId);
        while (current?.ReportsToStaffId is { } up && byId.TryGetValue(up, out var manager) && seen.Add(up) && chain.Count < MaxDepth)
        {
            chain.Add(manager);
            current = manager;
        }
        return chain;
    }

    /// <summary>How many levels sit below this person (0 for someone with no reports).</summary>
    public int DepthBelow(string staffId)
    {
        var deepest = 0;
        void Walk(string id, int level, HashSet<string> seen)
        {
            deepest = Math.Max(deepest, level);
            foreach (var r in reports[id]) if (seen.Add(r.Id)) Walk(r.Id, level + 1, seen);
        }
        Walk(staffId, 0, [staffId]);
        return deepest;
    }

    /// <summary>
    /// Whether <paramref name="staffId"/> may be set to report to <paramref name="managerId"/>. Returns a sentence the
    /// person can act on, or null when it is allowed.
    /// </summary>
    public string? ValidateManager(string staffId, string role, string? managerId)
    {
        if (role == PlatformStaffRoles.SuperAdmin)
            return managerId is null ? null : "A SuperAdmin does not report to anyone.";
        if (role != PlatformStaffRoles.Representative)
            return managerId is null ? null : "Only representatives report to someone.";

        if (string.IsNullOrWhiteSpace(managerId))
            return "A representative must report to a SuperAdmin or to another representative.";
        if (managerId == staffId) return "Nobody can report to themselves.";

        if (!byId.TryGetValue(managerId, out var manager)) return "That manager could not be found.";
        if (manager.IsDisabled) return "That manager's account is disabled.";
        if (manager.Role is not (PlatformStaffRoles.SuperAdmin or PlatformStaffRoles.Representative))
            return "A representative reports to a SuperAdmin or to another representative.";

        // Reporting to someone who already reports (however indirectly) to this person would close a loop.
        if (SelfAndDescendants(staffId).Contains(managerId)) return "That would make a loop: that person already reports to them.";

        // Depth: the new manager's chain plus this person's own subtree must stay within the limit.
        var depth = ManagersOf(managerId).Count + 1 + DepthBelow(staffId);
        if (depth > MaxDepth) return $"The team can be at most {MaxDepth} levels deep.";
        return null;
    }
}
