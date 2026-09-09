using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

/// <summary>
/// Daily-ish sweep (same PeriodicTimer/per-institution-scope shape as
/// DigestSchedulerService — see that file for the rationale) that turns
/// "it's a member's birthday today" into an auto-approved Spotlight, for any
/// institution that has opted into InstitutionFeatures.BirthdaySpotlight.
///
/// Only month/day of Member.DateOfBirth ever matters — the year is never
/// checked or shown. When several members share a day, they get ONE
/// combined spotlight rather than N separate ones (see BuildCopy): a feed
/// full of near-duplicate "happy birthday" cards would just be noise, and a
/// single celebratory post naming everyone reads as more intentional.
///
/// A 6-hour tick (not exactly midnight) means the spotlight can appear
/// anywhere from just after midnight UTC to a few hours in — acceptable for
/// something celebratory rather than time-critical; the per-institution
/// same-day dedupe check (via Spotlight.FeaturedMonth) is what actually
/// prevents duplicates, not the tick cadence.
/// </summary>
public class BirthdaySpotlightSchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<BirthdaySpotlightSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(TickInterval);
        do
        {
            await RunCycleAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        List<string> institutionIds;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AlumniDbContext>();
            institutionIds = await db.Institutions.IgnoreQueryFilters()
                .Where(i => i.Status == "Active" && !i.DisabledFeatures.Contains(InstitutionFeatures.BirthdaySpotlight))
                .Select(i => i.Id)
                .ToListAsync(stoppingToken);
        }

        logger.LogInformation("Birthday spotlight scheduler cycle starting for {Count} institutions", institutionIds.Count);

        var today = DateTime.UtcNow.Date;

        foreach (var institutionId in institutionIds)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ICurrentTenantService>().SetInstitutionId(institutionId);
                var memberRepo = scope.ServiceProvider.GetRequiredService<IAlumniPgRepository<MemberEntity>>();
                var spotlightRepo = scope.ServiceProvider.GetRequiredService<IAlumniPgRepository<Spotlight>>();

                var alreadyRanToday = await spotlightRepo.GetOneAsync(
                    s => s.Type == "Birthday" && s.FeaturedMonth == today) is not null;
                if (alreadyRanToday) continue;

                var celebrants = (await memberRepo.GetAllAsync(m =>
                        m.DateOfBirth != null && m.Status == "Active"))
                    .Where(m => m.DateOfBirth!.Value.Month == today.Month && m.DateOfBirth.Value.Day == today.Day)
                    .OrderBy(m => m.FirstName)
                    .ToList();
                if (celebrants.Count == 0) continue;

                var snapshots = celebrants.Select(m => new MemberSnapshot
                {
                    Id = m.Id,
                    FirstName = m.FirstName,
                    LastName = m.LastName,
                    Email = m.Email,
                    ProfilePictureUrl = m.ProfilePictureUrl,
                    MemberNumber = m.MemberNumber,
                }).ToList();

                var (title, story) = BuildCopy(celebrants);

                await spotlightRepo.AddAsync(new Spotlight
                {
                    InstitutionId = institutionId,
                    MemberId = celebrants[0].Id,
                    Member = snapshots[0],
                    Type = "Birthday",
                    MemberIds = celebrants.Select(m => m.Id).ToList(),
                    Members = snapshots,
                    Title = title,
                    Story = story,
                    Status = "Approved",
                    FeaturedMonth = today,
                    CreatedBy = "system",
                });

                logger.LogInformation("Created birthday spotlight for institution {InstitutionId} covering {Count} member(s)", institutionId, celebrants.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Birthday spotlight cycle failed for institution {InstitutionId}", institutionId);
            }
        }
    }

    private static (string Title, string Story) BuildCopy(List<MemberEntity> celebrants)
    {
        var names = celebrants.Select(m => m.FirstName).ToList();
        if (celebrants.Count == 1)
        {
            var m = celebrants[0];
            return ($"Happy Birthday, {m.FirstName} {m.LastName}!",
                $"Join us in wishing {m.FirstName} {m.LastName} a very happy birthday today!");
        }

        if (celebrants.Count == 2)
        {
            return ($"Happy Birthday, {names[0]} & {names[1]}!",
                $"Two birthdays today! Join us in wishing {names[0]} {celebrants[0].LastName} and {names[1]} {celebrants[1].LastName} a very happy birthday.");
        }

        var fullNames = celebrants.Select(m => $"{m.FirstName} {m.LastName}").ToList();
        var listed = string.Join(", ", fullNames[..^1]) + $", and {fullNames[^1]}";
        return ($"Happy Birthday to {celebrants.Count} of our alumni today!",
            $"It's a big day for {celebrants.Count} members of our community: {listed}. Join us in wishing them all a very happy birthday!");
    }
}
