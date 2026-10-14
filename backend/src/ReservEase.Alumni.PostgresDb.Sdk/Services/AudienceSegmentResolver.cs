using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.PostgresDb.Sdk.Services;

/// <summary>Who an administrator may reach: everyone, or only the year groups and groups they look after.</summary>
public record SegmentScope(bool Unrestricted, IReadOnlyCollection<int> YearGroups, IReadOnlyCollection<string> CommunityIds)
{
    public static readonly SegmentScope Everyone = new(true, [], []);
}

/// <summary>
/// Turns a saved rule into the members it describes right now. One implementation serves messages, reports and anything
/// else that wants an audience, so "who is in this segment" never differs between them.
/// </summary>
public class AudienceSegmentResolver(
    IAlumniPgRepository<Member> memberRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    IAlumniPgRepository<Community> communityRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<Campaign> campaignRepo)
{
    private const int DormantDays = 60;

    /// <param name="institutionId">
    /// Give this when running outside a request (the worker has no tenant): every lookup then names the institution
    /// itself instead of relying on the tenant filter. Leave it out inside a request.
    /// </param>
    public async Task<List<Member>> ResolveAsync(SegmentCriteria criteria, SegmentScope scope, DateTime? now = null, string? institutionId = null)
    {
        var explicitTenant = !string.IsNullOrEmpty(institutionId);
        var at = now ?? DateTime.UtcNow;
        var status = string.IsNullOrWhiteSpace(criteria.Status) ? null : criteria.Status;
        var dormantBefore = at.AddDays(-DormantDays);
        var joinedAfter = criteria.JoinedWithinDays is > 0 ? at.AddDays(-criteria.JoinedWithinDays.Value) : (DateTime?)null;
        var answer = criteria.AnswerContains?.Trim().ToLower();

        // Who a scoped administrator is allowed to reach at all (their year groups, or approved members of their groups).
        HashSet<string>? scopeMemberIds = null;
        if (!scope.Unrestricted && scope.CommunityIds.Count > 0)
        {
            var ids = scope.CommunityIds.ToList();
            scopeMemberIds = (await membershipRepo.GetAllAsync(m => (!explicitTenant || m.InstitutionId == institutionId) && ids.Contains(m.CommunityId) && m.Status == "Approved", explicitTenant))
                .Select(m => m.MemberId).ToHashSet();
        }
        var years = scope.YearGroups.ToList();
        var unrestricted = scope.Unrestricted;
        var dormantOnly = criteria.DormantOnly;
        var from = criteria.GraduationYearFrom;
        var to = criteria.GraduationYearTo;
        var dept = string.IsNullOrWhiteSpace(criteria.DepartmentId) ? null : criteria.DepartmentId;

        var members = (await memberRepo.GetAllAsync(m =>
                (!explicitTenant || m.InstitutionId == institutionId)
                && (status == null ? m.Status != "Deleted" : m.Status == status)
                && (dept == null || m.DepartmentId == dept)
                && (!from.HasValue || m.GraduationYear >= from.Value)
                && (!to.HasValue || m.GraduationYear <= to.Value)
                && (!dormantOnly || m.LastLoginAt == null || m.LastLoginAt < dormantBefore)
                && (!joinedAfter.HasValue || m.CreatedAt >= joinedAfter.Value), explicitTenant))
            .ToList();

        if (!unrestricted)
            members = members.Where(m => years.Contains(m.GraduationYear) || (scopeMemberIds?.Contains(m.Id) ?? false)).ToList();

        // Groups, by id and by kind: a member is in the segment if they are an approved member of any of them.
        var groupIds = criteria.GroupIds.ToHashSet();
        if (criteria.GroupKinds.Count > 0)
        {
            var kinds = criteria.GroupKinds.Select(k => k.Trim().ToLower()).ToHashSet();
            foreach (var c in await communityRepo.GetAllAsync(c => (!explicitTenant || c.InstitutionId == institutionId) && c.Kind != null, explicitTenant))
                if (c.Kind is not null && kinds.Contains(c.Kind.Trim().ToLower())) groupIds.Add(c.Id);
        }
        if (criteria.GroupIds.Count > 0 || criteria.GroupKinds.Count > 0)
        {
            if (groupIds.Count == 0) return [];
            var ids = groupIds.ToList();
            var inGroups = (await membershipRepo.GetAllAsync(m => (!explicitTenant || m.InstitutionId == institutionId) && ids.Contains(m.CommunityId) && m.Status == "Approved", explicitTenant))
                .Select(m => m.MemberId).ToHashSet();
            members = members.Where(m => inGroups.Contains(m.Id)).ToList();
        }

        if (!string.IsNullOrEmpty(answer))
            members = members.Where(m => m.CustomFields is not null && m.CustomFields.Values.Any(v => v.ToLower().Contains(answer))).ToList();

        if (!string.IsNullOrEmpty(criteria.DuesStatus) && members.Count > 0)
            members = await ApplyDuesAsync(members, criteria.DuesStatus!, at.Year, institutionId);

        return members;
    }

    /// <summary>
    /// "Paid" / "Unpaid" is judged against this year's membership dues. When the community has not set dues for this year
    /// there is nothing to be unpaid against, so Unpaid describes no one and Paid describes no one rather than everyone.
    /// </summary>
    private async Task<List<Member>> ApplyDuesAsync(List<Member> members, string status, int year, string? institutionId)
    {
        var explicitTenant = !string.IsNullOrEmpty(institutionId);
        var campaignIds = (await campaignRepo.GetAllAsync(c => (!explicitTenant || c.InstitutionId == institutionId) && c.IsMembershipCampaign && c.MembershipYear == year, explicitTenant))
            .Select(c => c.Id).ToList();
        if (campaignIds.Count == 0) return [];

        var paid = (await contributionRepo.GetAllAsync(c => (!explicitTenant || c.InstitutionId == institutionId) && c.Status == "Successful" && campaignIds.Contains(c.CampaignId), explicitTenant))
            .Select(c => c.MemberId).ToHashSet();

        return status == SegmentDuesStatuses.Paid
            ? members.Where(m => paid.Contains(m.Id)).ToList()
            : members.Where(m => !paid.Contains(m.Id)).ToList();
    }
}
