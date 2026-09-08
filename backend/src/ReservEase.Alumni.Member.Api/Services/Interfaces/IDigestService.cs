namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

/// <summary>Assembles and sends the re-engagement digest email for whichever members of the current tenant are due one.</summary>
public interface IDigestService
{
    /// <summary>
    /// Sends a digest to every active member of the current tenant whose
    /// digest preference is due (Weekly/Monthly cadence, or never sent).
    /// Must be called with ICurrentTenantService already set to the target
    /// institution. Returns the number of members actually emailed.
    /// </summary>
    Task<int> SendDueDigestsAsync();
}
