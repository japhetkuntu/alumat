using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using ReservEase.Alumni.Platform.Api.Controllers;
using Xunit;
using Xunit.Abstractions;

namespace ReservEase.Alumni.Platform.Api.Tests;

/// <summary>
/// A representative is a signed-in staff member, so any action that only says [Authorize] is open to
/// them. This pins down exactly which actions that is, so a new endpoint can't quietly expose
/// platform-wide data to the role.
/// </summary>
public class RepresentativeSurfaceTests(ITestOutputHelper output)
{
    private static readonly HashSet<string> OpenToAnyStaff = new(StringComparer.Ordinal)
    {
        // Own session and notifications.
        "AuthController.ChangePassword", "AuthController.GetCurrentStaff",
        "PlatformNotificationsController.GetNotifications", "PlatformNotificationsController.GetUnreadCount",
        "PlatformNotificationsController.MarkAllRead", "PlatformNotificationsController.MarkRead",
        // Reference data and switches that carry no institution or money figures.
        "FeaturesController.GetCatalog", "PlatformSettingsController.Get",
        "InstitutionsController.CheckSlug", "InstitutionsController.GetBanks",
        "InstitutionsController.GetBaseDomains", "InstitutionsController.ResolveAccount",
        // Narrowed to the representative's own team in the service (StaffScope), tested in RepresentativeScopeTests.
        "InstitutionsController.GetInstitution", "InstitutionsController.GetInstitutions",
        "InstitutionsController.GetInstitutionStaff",
        // Each report's definition lists the roles that may run it; Representative is on none.
        "ReportsController.DownloadReport", "ReportsController.GetCatalog",
        "ReportsController.GetReportJobs", "ReportsController.RequestReport",
    };

    private static IEnumerable<string> ActionsOpenToAnyStaff()
    {
        var controllers = typeof(DefaultController).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));
        foreach (var c in controllers)
        {
            var classAuth = c.GetCustomAttributes<AuthorizeAttribute>(true).ToList();
            foreach (var m in c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!m.GetCustomAttributes().Any(a => a is HttpMethodAttribute)) continue;
                if (m.GetCustomAttribute<AllowAnonymousAttribute>() != null) continue;
                var auth = m.GetCustomAttributes<AuthorizeAttribute>(true).Concat(classAuth).ToList();
                if (auth.Count == 0) continue; // anonymous or authenticated by other means
                var restricted = auth.Any(a => !string.IsNullOrWhiteSpace(a.Roles));
                if (!restricted) yield return $"{c.Name}.{m.Name}";
            }
        }
    }

    [Fact]
    public void Actions_open_to_any_signed_in_staff_are_the_known_safe_ones()
    {
        var open = ActionsOpenToAnyStaff().OrderBy(x => x).ToList();
        foreach (var o in open) output.WriteLine(o);
        var unexpected = open.Where(o => !OpenToAnyStaff.Contains(o)).ToList();
        Assert.True(unexpected.Count == 0, "Open to every staff role, representatives included: " + string.Join(", ", unexpected));
    }
}
