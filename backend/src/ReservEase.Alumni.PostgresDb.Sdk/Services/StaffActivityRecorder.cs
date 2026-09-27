using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using Microsoft.Extensions.Logging;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.PostgresDb.Sdk.Services;

/// <summary>Records institution staff portal usage per week — see <see cref="StaffActivityWeek"/>.</summary>
public interface IStaffActivityRecorder
{
    /// <summary>Marks <paramref name="staffId"/> active for the week containing <paramref name="now"/>. Idempotent; never throws.</summary>
    Task RecordAsync(string institutionId, string staffId, DateTime now);
}

public class StaffActivityRecorder(
    IAlumniPgRepository<StaffActivityWeek> staffActivityRepo,
    ILogger<StaffActivityRecorder> logger) : IStaffActivityRecorder
{
    public async Task RecordAsync(string institutionId, string staffId, DateTime now)
    {
        try
        {
            var weekStart = InstitutionActivationService.WeekStart(now);
            var exists = await staffActivityRepo.GetQueryable(
                    a => a.InstitutionId == institutionId && a.StaffId == staffId && a.WeekStart == weekStart)
                .AnyAsync();
            if (!exists)
                await staffActivityRepo.AddAsync(new StaffActivityWeek { InstitutionId = institutionId, StaffId = staffId, WeekStart = weekStart, CreatedBy = staffId });
        }
        catch (Exception e)
        {
            // Usually a concurrent request (a second tab refreshing) inserted the same week
            // first — the unique index did its job. Activity tracking must never break sign-in.
            logger.LogDebug(e, "Staff activity for {StaffId} not recorded", staffId);
        }
    }
}
