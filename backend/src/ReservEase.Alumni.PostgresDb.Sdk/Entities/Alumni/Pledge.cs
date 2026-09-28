using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// A member's non-binding statement of intent to give to a fundraiser by a date. It moves no money and is never
/// counted in a campaign's collected total. Visible only to institution administrators, never to other members.
/// Whether it has been redeemed is worked out from the member's real, successful contributions to the same
/// campaign (see <see cref="PledgeProgress"/>), so there is no second place where "paid" can drift out of date.
/// </summary>
public class Pledge : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    public string CampaignId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    /// <summary>Kept on the row so the admin list does not need a join per pledge. Removed with the pledge when a member closes their account.</summary>
    public string MemberName { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }

    /// <summary>Open, Cancelled or WrittenOff. "Fulfilled", "Part-paid" and "Overdue" are derived, never stored.</summary>
    public string Status { get; set; } = PledgeStatuses.Open;
    public string? StatusNote { get; set; }

    public DateTime? UpcomingReminderSentAt { get; set; }
    public DateTime? DueReminderSentAt { get; set; }
    public DateTime? OverdueReminderSentAt { get; set; }
}

public static class PledgeStatuses
{
    public const string Open = "Open";
    public const string Cancelled = "Cancelled";
    public const string WrittenOff = "WrittenOff";
}

/// <summary>What a pledge looks like right now, for display and for deciding whether to remind.</summary>
public static class PledgeStates
{
    public const string Pledged = "Pledged";
    public const string PartPaid = "PartPaid";
    public const string Fulfilled = "Fulfilled";
    public const string Overdue = "Overdue";
    public const string Cancelled = "Cancelled";
    public const string WrittenOff = "WrittenOff";
}

public readonly record struct PledgeProgressResult(decimal Paid, decimal Outstanding, string State);

public static class PledgeProgress
{
    /// <summary>
    /// A pledge is redeemed by the member's own successful contributions to the same campaign made after the pledge
    /// was created, whether or not they came through a reminder link. Paying part of it leaves the rest outstanding.
    /// </summary>
    public static PledgeProgressResult Compute(Pledge pledge, IEnumerable<Contribution> contributionsByThisMemberToThisCampaign, DateTime nowUtc)
    {
        var paid = contributionsByThisMemberToThisCampaign
            .Where(c => c.Status == "Successful" && c.CreatedAt >= pledge.CreatedAt)
            .Sum(c => c.Amount);
        var outstanding = Math.Max(0m, pledge.Amount - paid);

        if (pledge.Status == PledgeStatuses.Cancelled) return new(paid, outstanding, PledgeStates.Cancelled);
        if (pledge.Status == PledgeStatuses.WrittenOff) return new(paid, outstanding, PledgeStates.WrittenOff);
        if (outstanding == 0m) return new(paid, 0m, PledgeStates.Fulfilled);
        if (pledge.DueDate < nowUtc) return new(paid, outstanding, PledgeStates.Overdue);
        return new(paid, outstanding, paid > 0m ? PledgeStates.PartPaid : PledgeStates.Pledged);
    }
}
