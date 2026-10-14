using ReservEase.Alumni.Platform.Api.Services.Implementations;

namespace ReservEase.Alumni.Platform.Api.Tests;

/// <summary>A caller whose visibility is decided by the test: everything by default, or only what some people own.</summary>
public sealed class FixedScope(StaffVisibility? visibility = null) : IStaffScope
{
    public Task<StaffVisibility> GetAsync() => Task.FromResult(visibility ?? StaffVisibility.Everything);

    public static FixedScope Everything() => new();

    public static FixedScope Representative(string staffId, params string[] alsoSees)
        => new(new StaffVisibility(false, staffId, new HashSet<string>([staffId, .. alsoSees])));
}
