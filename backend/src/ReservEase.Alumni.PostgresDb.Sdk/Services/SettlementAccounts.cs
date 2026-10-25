using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.PostgresDb.Sdk.Services;

/// <summary>
/// Which Paystack account a payment for a given audience settles into. A group (community) with an approved account of its own takes
/// the payments aimed at it; so does a year group, when the payment is aimed at that one year group alone. Anything else, and any
/// group or year group without a live account of its own, falls back to the institution's account, which the caller supplies.
/// One rule for every place that charges, so the payout figures and the charge itself can never disagree.
/// </summary>
public static class SettlementAccounts
{
    /// <summary>The live account of a group or year group, or false when it has none (never set up, pending, rejected, or set to use the institution's).</summary>
    public static bool IsLive(string payoutStatus, bool useInstitutionAccount, string? subaccountCode) =>
        payoutStatus == "Approved" && !useInstitutionAccount && !string.IsNullOrEmpty(subaccountCode);

    public const string InstitutionTarget = "institution";

    /// <summary>What a service's settlement choice means: pay the institution, or the audience (a group, or one year group) it names.</summary>
    public static (string? CommunityId, int? Year) AudienceOfService(string? settlementTarget, string? serviceCommunityId)
    {
        if (string.IsNullOrWhiteSpace(settlementTarget)) return (serviceCommunityId, null);
        if (settlementTarget == InstitutionTarget) return (null, null);
        if (settlementTarget.StartsWith("group:", StringComparison.Ordinal)) return (settlementTarget[6..], null);
        if (settlementTarget.StartsWith("year:", StringComparison.Ordinal) && int.TryParse(settlementTarget[5..], out var year)) return (null, year);
        return (serviceCommunityId, null);
    }

    /// <summary>The subaccount a paid service settles into, or null for the institution's account.</summary>
    public static Task<string?> ForServiceAsync(
        IAlumniPgRepository<Batch>? batchRepo, IAlumniPgRepository<Community>? communityRepo,
        string? institutionId, string? settlementTarget, string? serviceCommunityId)
    {
        var (communityId, year) = AudienceOfService(settlementTarget, serviceCommunityId);
        return ForAudienceAsync(batchRepo, communityRepo, institutionId, year is null ? null : [year.Value], communityId);
    }

    /// <summary>
    /// The subaccount code for a payment aimed at a group, or at exactly one year group. A group takes precedence (an audience is one or
    /// the other). Null means "use the institution's account".
    /// </summary>
    public static async Task<string?> ForAudienceAsync(
        IAlumniPgRepository<Batch>? batchRepo, IAlumniPgRepository<Community>? communityRepo,
        string? institutionId, IReadOnlyList<int>? yearGroups, string? communityId)
    {
        if (string.IsNullOrEmpty(institutionId)) return null;

        if (!string.IsNullOrEmpty(communityId))
        {
            if (communityRepo is null) return null;
            var group = await communityRepo.GetOneAsync(c => c.Id == communityId && c.InstitutionId == institutionId, ignoreQueryFilters: true);
            return group is not null && IsLive(group.PayoutStatus, group.UseInstitutionAccount, group.PaystackSubaccountCode) ? group.PaystackSubaccountCode : null;
        }

        if (yearGroups is { Count: 1 } && batchRepo is not null)
        {
            var year = yearGroups[0];
            var batch = await batchRepo.GetOneAsync(b => b.InstitutionId == institutionId && b.Year == year, ignoreQueryFilters: true);
            return batch is not null && IsLive(batch.PayoutStatus, batch.UseInstitutionAccount, batch.PaystackSubaccountCode) ? batch.PaystackSubaccountCode : null;
        }

        return null;
    }
}
