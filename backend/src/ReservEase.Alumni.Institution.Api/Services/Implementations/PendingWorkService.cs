using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

/// <summary>
/// One place that answers "what are members waiting on me for?". Members submit requests in several places (joining, suggesting
/// an opportunity, listing a business, a spotlight story, offering to mentor); each is reviewed on its own page, so without this an
/// administrator only finds out by remembering to look. Counts come from the tenant-filtered repositories, and a feature the
/// institution has switched off is left out because the administrator cannot reach its page.
/// </summary>
public class PendingWorkService(
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Job> jobRepo,
    IAlumniPgRepository<BusinessListing> businessRepo,
    IAlumniPgRepository<Spotlight> spotlightRepo,
    IAlumniPgRepository<MentorProfile> mentorRepo) : IPendingWorkService
{
    public async Task<IReadOnlyList<PendingWorkItemDto>> GetAsync(IReadOnlyCollection<string> disabledFeatures)
    {
        bool On(string feature) => !disabledFeatures.Contains(feature);
        var items = new List<PendingWorkItemDto>();

        void Add(string key, string singular, string plural, int count, string url)
        {
            if (count > 0) items.Add(new PendingWorkItemDto(key, count == 1 ? singular : plural.Replace("{n}", count.ToString()), count, url));
        }

        Add("members", "1 new member waiting for approval", "{n} new members waiting for approval",
            await memberRepo.CountAsync(m => m.Status == "Pending"), "/members?status=Pending");
        if (On("Jobs"))
            Add("opportunities", "1 suggested opportunity to review", "{n} suggested opportunities to review",
                await jobRepo.CountAsync(j => j.Status == "Pending"), "/jobs?status=Pending");
        if (On("BusinessDirectory"))
            Add("businesses", "1 business listing to review", "{n} business listings to review",
                await businessRepo.CountAsync(b => b.Status == "Pending" || b.HasPendingEdit), "/business-directory?status=Pending");
        if (On("Spotlights"))
            Add("spotlights", "1 spotlight story to review", "{n} spotlight stories to review",
                await spotlightRepo.CountAsync(s => s.Status == "Pending"), "/spotlights?status=Pending");
        if (On("Mentorship"))
            Add("mentors", "1 mentor profile to review", "{n} mentor profiles to review",
                await mentorRepo.CountAsync(p => p.Status == "Pending"), "/mentorship?status=Pending");
        return items;
    }
}
