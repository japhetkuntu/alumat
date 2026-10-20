using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public record SampleContentStatus(bool Present, int NewsPosts, int Events);

public interface ISampleContentService
{
    Task<IApiResponse<SampleContentStatus>> GetStatusAsync();
    Task<IApiResponse<SampleContentStatus>> AddAsync(string adminId);
    Task<IApiResponse<SampleContentStatus>> RemoveAsync();
}

/// <summary>
/// A new community opens to an empty portal, which looks abandoned to the first members who arrive. This adds a
/// small, clearly marked set of example content on request, and removes exactly that content again in one step.
/// Everything it creates carries <see cref="Marker"/> as its creator, so nothing a person wrote can ever be removed by it.
/// Nothing is created without an administrator asking, and nobody is notified about it.
/// </summary>
public class SampleContentService(
    IAlumniPgRepository<NewsPost> newsRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IRedisService<PublicContentCacheConfig> publicCache,
    ICurrentTenantService tenant,
    ILogger<SampleContentService> logger) : ISampleContentService
{
    /// <summary>The creator recorded on every sample item. Never a real account id.</summary>
    public const string Marker = "sample-content";

    public async Task<IApiResponse<SampleContentStatus>> GetStatusAsync()
    {
        try { return (await CountAsync()).ToOkApiResponse(); }
        catch (Exception e)
        {
            logger.LogError(e, "Error reading sample content status");
            return ApiResponseExtensions.ToServerErrorApiResponse<SampleContentStatus>("Failed to read sample content");
        }
    }

    public async Task<IApiResponse<SampleContentStatus>> AddAsync(string adminId)
    {
        try
        {
            var existing = await CountAsync();
            if (existing.Present)
                return ApiResponseExtensions.ToBadRequestApiResponse<SampleContentStatus>("Sample content is already added. Remove it first to add it again.");

            var now = DateTime.UtcNow;
            await newsRepo.AddRangeAsync(
            [
                new NewsPost
                {
                    Title = "Welcome to our community", Category = "Announcement", IsPinned = true, Status = "Published", PublishedAt = now,
                    AuthorId = adminId, CreatedBy = Marker,
                    Content = "This is an example post. Welcome, and thank you for joining. This is where we will share news, "
                        + "celebrate our members and tell you what is coming up. Administrators can edit or delete this post, "
                        + "or remove all of the example content in one step from the dashboard.",
                },
                new NewsPost
                {
                    Title = "How to get the most from this portal", Category = "Guide", Status = "Published", PublishedAt = now.AddMinutes(-1),
                    AuthorId = adminId, CreatedBy = Marker,
                    Content = "This is an example post. Complete your profile, find other members in the directory, RSVP to events and "
                        + "follow the groups that matter to you.",
                },
            ]);

            var start = DateTime.SpecifyKind(now.Date.AddDays(21).AddHours(15), DateTimeKind.Utc);
            await eventRepo.AddAsync(new AlumniEvent
            {
                Title = "Example event: our first gathering", Venue = "To be announced", Status = "Upcoming",
                Description = "This is an example event so you can see how events look to members. Edit it with the real details, or remove it.",
                StartDate = start, EndDate = start.AddHours(2), CreatedBy = Marker,
            });

            await InvalidateAsync();
            logger.LogInformation("Sample content added by admin {AdminId}", adminId);
            return (await CountAsync()).ToCreatedApiResponse("Example content added");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error adding sample content");
            return ApiResponseExtensions.ToServerErrorApiResponse<SampleContentStatus>("Failed to add example content");
        }
    }

    public async Task<IApiResponse<SampleContentStatus>> RemoveAsync()
    {
        try
        {
            foreach (var post in (await newsRepo.GetAllAsync(n => n.CreatedBy == Marker)).ToList())
                await newsRepo.RemoveAsync(post);
            foreach (var ev in (await eventRepo.GetAllAsync(x => x.CreatedBy == Marker)).ToList())
                await eventRepo.RemoveAsync(ev);

            await InvalidateAsync();
            return (await CountAsync()).ToOkApiResponse("Example content removed");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error removing sample content");
            return ApiResponseExtensions.ToServerErrorApiResponse<SampleContentStatus>("Failed to remove example content");
        }
    }

    private async Task<SampleContentStatus> CountAsync()
    {
        var news = await newsRepo.CountAsync(n => n.CreatedBy == Marker);
        var events = await eventRepo.CountAsync(e => e.CreatedBy == Marker);
        return new SampleContentStatus(news + events > 0, news, events);
    }

    private async Task InvalidateAsync()
    {
        if (tenant.InstitutionId is not { } id) return;
        await publicCache.RemoveAsync(PublicContentCacheKeys.News(id));
        await publicCache.RemoveAsync(PublicContentCacheKeys.Events(id));
    }
}
