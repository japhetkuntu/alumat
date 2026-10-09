namespace ReservEase.Alumni.Institution.Api.Services.Interfaces;

/// <summary>One kind of member request that is waiting for an administrator, with where to go to deal with it.</summary>
public record PendingWorkItemDto(string Key, string Label, int Count, string ActionUrl);

public interface IPendingWorkService
{
    /// <summary>Every kind of member request currently waiting for review, only those with something waiting, oldest need first.</summary>
    Task<IReadOnlyList<PendingWorkItemDto>> GetAsync(IReadOnlyCollection<string> disabledFeatures);
}
