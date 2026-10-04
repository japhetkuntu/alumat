using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

/// <summary>
/// The portal home: what named people have done lately, and which modules have anything in them.
/// Every query here repeats the visibility rule of the module it reads from (year-group targeting,
/// community membership, approval status), so home never shows a member something that module's
/// own page would hide from them.
/// </summary>
public class HomeService(
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    IAlumniPgRepository<Community> communityRepo,
    IAlumniPgRepository<Spotlight> spotlightRepo,
    IAlumniPgRepository<ForumThread> threadRepo,
    IAlumniPgRepository<MentorProfile> mentorRepo,
    IAlumniPgRepository<BusinessListing> listingRepo,
    IAlumniPgRepository<Job> jobRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<NewsPost> newsRepo,
    IAlumniPgRepository<Resource> resourceRepo,
    IAlumniPgRepository<PhotoAlbum> albumRepo,
    IAlumniPgRepository<StoreProduct> productRepo,
    IAlumniPgRepository<StoreOrder> orderRepo,
    IAlumniPgRepository<ServiceType> serviceTypeRepo,
    ILogger<HomeService> logger) : IHomeService
{
    /// <summary>How far back the feed reaches. Older than this is history, not news.</summary>
    private const int WindowDays = 60;
    /// <summary>Cap per kind of activity, so one busy module (a bulk member import, say) can't crowd out the rest.</summary>
    private const int PerSource = 8;
    private const int MaxItems = 20;

    private record Activity(string Kind, string? EntityId, string PersonId, string? Title, DateTime OccurredAt);

    private async Task<List<string>> GetApprovedCommunityIdsAsync(string memberId)
    {
        var memberships = await membershipRepo.GetAllAsync(m => m.MemberId == memberId && m.Status == "Approved");
        return memberships.Select(m => m.CommunityId).ToList();
    }

    public async Task<IApiResponse<HomeFeedDto>> GetFeedAsync(string memberId, IReadOnlyCollection<string> disabledFeatures)
    {
        try
        {
            var me = await memberRepo.GetByIdAsync(memberId);
            if (me is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<HomeFeedDto>("Member not found");

            bool On(string feature) => !disabledFeatures.Contains(feature);
            var cutoff = DateTime.UtcNow.AddDays(-WindowDays);
            var myYear = me.GraduationYear;
            var activities = new List<Activity>();

            if (On(InstitutionFeatures.Directory))
            {
                // Classmates first: with more joiners than the cap, the ones a member knows are the ones worth keeping.
                var joined = await memberRepo.GetQueryable(m => m.Status == "Active" && m.Id != memberId && m.CreatedAt >= cutoff)
                    .OrderByDescending(m => m.GraduationYear == myYear).ThenByDescending(m => m.CreatedAt)
                    .Take(PerSource).ToListAsync();
                activities.AddRange(joined.Select(m => new Activity(HomeFeedKinds.MemberJoined, null, m.Id, null, m.CreatedAt)));
            }

            // Birthday spotlights also open a forum thread in each celebrant's name — one line per birthday, not two.
            var birthdayThreadIds = new HashSet<string>();
            if (On(InstitutionFeatures.Spotlights))
            {
                var spotlights = await spotlightRepo.GetQueryable(s => s.Status == "Approved" && s.CreatedAt >= cutoff)
                    .OrderByDescending(s => s.CreatedAt).Take(PerSource).ToListAsync();
                foreach (var s in spotlights)
                {
                    if (s.Type == "Birthday")
                    {
                        birthdayThreadIds.UnionWith(s.ForumThreadIds);
                        var celebrants = s.MemberIds.Count > 0 ? s.MemberIds : [s.MemberId];
                        activities.AddRange(celebrants.Select(id => new Activity(HomeFeedKinds.Birthday, s.Id, id, null, s.CreatedAt)));
                    }
                    else
                    {
                        activities.Add(new Activity(HomeFeedKinds.Spotlight, s.Id, s.MemberId, s.Title, s.CreatedAt));
                    }
                }
            }

            if (On(InstitutionFeatures.Forum))
            {
                var communityIds = await GetApprovedCommunityIdsAsync(memberId);
                var threads = await threadRepo.GetQueryable(t => !t.IsClosed && t.CreatedAt >= cutoff
                        && (t.CommunityId == null || communityIds.Contains(t.CommunityId)))
                    .OrderByDescending(t => t.CreatedAt).Take(PerSource + birthdayThreadIds.Count).ToListAsync();
                activities.AddRange(threads.Where(t => !birthdayThreadIds.Contains(t.Id)).Take(PerSource)
                    .Select(t => new Activity(HomeFeedKinds.ForumThread, t.Id, t.AuthorId, t.Title, t.CreatedAt)));
            }

            if (On(InstitutionFeatures.Mentorship))
            {
                var mentors = await mentorRepo.GetQueryable(p => p.Status == "Approved" && p.CurrentMenteeCount < p.MaxMentees && p.CreatedAt >= cutoff)
                    .OrderByDescending(p => p.CreatedAt).Take(PerSource).ToListAsync();
                activities.AddRange(mentors.Select(p => new Activity(HomeFeedKinds.MentorJoined, p.Id, p.MemberId, p.Area, p.CreatedAt)));
            }

            if (On(InstitutionFeatures.BusinessDirectory))
            {
                // Admin-added listings have no owning member, so there is nobody to name.
                var listings = await listingRepo.GetQueryable(l => l.Status == "Approved" && !l.IsHiddenByMember && l.MemberId != null && l.CreatedAt >= cutoff)
                    .OrderByDescending(l => l.CreatedAt).Take(PerSource).ToListAsync();
                activities.AddRange(listings.Select(l => new Activity(HomeFeedKinds.BusinessListed, l.Id, l.MemberId!, l.BusinessName, l.CreatedAt)));
            }

            // Names and photos come from the live Member rows, not the snapshots frozen on each record,
            // and anyone who is no longer an active member drops out of the feed with their activity.
            var personIds = activities.Select(a => a.PersonId).Where(id => id != memberId).Distinct().ToList();
            var people = (await memberRepo.GetAllAsync(m => personIds.Contains(m.Id) && m.Status == "Active")).ToDictionary(m => m.Id);

            var items = activities
                .Where(a => people.ContainsKey(a.PersonId))
                .OrderByDescending(a => a.OccurredAt)
                .Take(MaxItems)
                .Select(a =>
                {
                    var person = people[a.PersonId];
                    return new HomeFeedItemDto
                    {
                        Kind = a.Kind,
                        EntityId = a.EntityId,
                        PersonId = person.Id,
                        PersonName = $"{person.FirstName} {person.LastName}".Trim(),
                        PersonPhotoUrl = person.ProfilePictureUrl,
                        // Community-type institutions don't collect a graduation year; 0 is "none", not a shared class.
                        PersonGraduationYear = person.GraduationYear > 0 ? person.GraduationYear : null,
                        SameYearGroup = myYear > 0 && person.GraduationYear == myYear,
                        SameDepartment = !string.IsNullOrEmpty(me.DepartmentId) && person.DepartmentId == me.DepartmentId,
                        Title = a.Title,
                        OccurredAt = a.OccurredAt,
                    };
                })
                .ToList();

            return new HomeFeedDto { LastSeenAt = me.HomeSeenAt, Items = items }.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error building home feed for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<HomeFeedDto>("Failed to load your home feed");
        }
    }

    public async Task<IApiResponse<HomeModulesDto>> GetModulesAsync(string memberId, IReadOnlyCollection<string> disabledFeatures)
    {
        try
        {
            var me = await memberRepo.GetByIdAsync(memberId);
            if (me is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<HomeModulesDto>("Member not found");

            var myYear = me.GraduationYear;
            var communityIds = await GetApprovedCommunityIdsAsync(memberId);

            // A switched-off feature is neither empty nor full — the portal already hides it.
            var checks = new (string Feature, Func<Task<bool>> HasContent)[]
            {
                (InstitutionFeatures.Jobs, () => jobRepo.GetQueryable(j => j.Status == "Active"
                    && (j.CommunityId == null || communityIds.Contains(j.CommunityId))
                    && (j.YearGroups == null || j.YearGroups.Count == 0 || j.YearGroups.Contains(myYear))).AnyAsync()),
                (InstitutionFeatures.Events, () => eventRepo.GetQueryable(e => e.Status != "Cancelled"
                    && (e.CommunityId == null || communityIds.Contains(e.CommunityId))
                    && (e.YearGroups == null || e.YearGroups.Count == 0 || e.YearGroups.Contains(myYear))).AnyAsync()),
                (InstitutionFeatures.News, () => newsRepo.GetQueryable(p => p.Status == "Published"
                    && (p.CommunityId == null || communityIds.Contains(p.CommunityId))
                    && (p.YearGroups == null || p.YearGroups.Count == 0 || p.YearGroups.Contains(myYear))).AnyAsync()),
                (InstitutionFeatures.Forum, () => threadRepo.GetQueryable(t => !t.IsClosed
                    && (t.CommunityId == null || communityIds.Contains(t.CommunityId))).AnyAsync()),
                (InstitutionFeatures.Mentorship, () => mentorRepo.GetQueryable(p => p.Status == "Approved").AnyAsync()),
                (InstitutionFeatures.Resources, () => resourceRepo.GetQueryable(r => r.CommunityId == null || communityIds.Contains(r.CommunityId)).AnyAsync()),
                (InstitutionFeatures.PhotoAlbums, () => albumRepo.GetQueryable(a => (a.CommunityId == null && a.YearGroups == null)
                    || (a.CommunityId != null && communityIds.Contains(a.CommunityId))
                    || (a.YearGroups != null && a.YearGroups.Contains(myYear))).AnyAsync()),
                (InstitutionFeatures.BusinessDirectory, () => listingRepo.GetQueryable(l => l.Status == "Approved" && !l.IsHiddenByMember).AnyAsync()),
                (InstitutionFeatures.Spotlights, () => spotlightRepo.GetQueryable(s => s.Status == "Approved").AnyAsync()),
                (InstitutionFeatures.Store, () => productRepo.GetQueryable(p => p.Status == "Active").AnyAsync()),
                (InstitutionFeatures.Services, () => serviceTypeRepo.GetQueryable(s => s.Status == "Active").AnyAsync()),
                (InstitutionFeatures.Communities, () => communityRepo.GetQueryable(c => c.IsActive).AnyAsync()),
            };

            var empty = new List<string>();
            foreach (var (feature, hasContent) in checks)
                if (!disabledFeatures.Contains(feature) && !await hasContent())
                    empty.Add(feature);

            var hasStoreOrders = !disabledFeatures.Contains(InstitutionFeatures.Store)
                && await orderRepo.GetQueryable(o => o.MemberId == memberId).AnyAsync();

            return new HomeModulesDto { Empty = empty, HasStoreOrders = hasStoreOrders }.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error checking module activity for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<HomeModulesDto>("Failed to check module activity");
        }
    }

    public async Task<IApiResponse<object>> MarkSeenAsync(string memberId)
    {
        try
        {
            var now = DateTime.UtcNow;
            await memberRepo.ExecuteUpdateAsync(m => m.Id == memberId, s => s.SetProperty(m => m.HomeSeenAt, now));
            return ((object)new { SeenAt = now }).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error marking home seen for member {MemberId}", memberId);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to record your visit");
        }
    }
}
