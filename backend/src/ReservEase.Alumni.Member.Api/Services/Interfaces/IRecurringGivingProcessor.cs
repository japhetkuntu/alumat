namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

/// <summary>Charges every standing monthly gift of the current tenant that's come due.</summary>
public interface IRecurringGivingProcessor
{
    /// <summary>
    /// Must be called with ICurrentTenantService already set to the target
    /// institution. Returns the number of gifts successfully charged.
    /// </summary>
    Task<int> ChargeDueRecurringGivingAsync();
}
