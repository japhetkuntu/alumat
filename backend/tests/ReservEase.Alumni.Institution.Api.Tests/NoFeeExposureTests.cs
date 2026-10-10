using System.Reflection;
using System.Text.RegularExpressions;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Engagement;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.Reports.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>
/// Institutions receive their full amount, so no fee, deduction or "net" figure is ever shown to their administrators: not
/// in an API response, a report column or a report description. These tests fail loudly if one is added, so it cannot creep
/// back in unnoticed. The platform team's own API and reports are a different audience and are not scanned here.
/// </summary>
public class NoFeeExposureTests
{
    // "Net" is matched as a word start (NetAmount, NetToInstitution), not as a substring of words like Network or Internet.
    private static readonly Regex Forbidden = new(
        @"fee|commission|platformrevenue|gatewaycharge|grosscharge|transactioncharge|deduct|^net[A-Z]|net(amount|to|payout)|reachedus|reached us",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static IEnumerable<Type> InstitutionFacingTypes()
    {
        var assemblies = new[] { typeof(IPendingWorkService).Assembly, typeof(ReservEase.Alumni.Common.Sdk.Models.JobDto).Assembly, typeof(MonthFigures).Assembly };
        return assemblies.SelectMany(a => a.GetTypes())
            .Where(t => t.IsPublic && (t.Namespace ?? "").Contains("Models") || t == typeof(MonthFigures) || (t.Namespace ?? "").EndsWith("Institution.Api.Engagement"));
    }

    [Fact]
    public void No_response_type_the_institution_portal_receives_carries_a_fee_or_net_amount()
    {
        // Guard the guard: if the scan stopped finding the response types it would pass for the wrong reason.
        Assert.Contains(typeof(MonthFigures), InstitutionFacingTypes());
        Assert.True(InstitutionFacingTypes().Count() > 40);

        var offenders = InstitutionFacingTypes()
            // Request bodies the platform team sends, and entities that only exist to be stored, are not responses.
            .Where(t => !t.Name.EndsWith("Request") && !t.Namespace!.Contains("PostgresDb"))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => $"{t.Name}.{p.Name}"))
            .Where(n => Forbidden.IsMatch(n.Split('.')[1]))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void No_institution_report_names_a_fee_or_net_amount_in_what_it_says_it_shows()
    {
        var institution = ReportCatalog.All.Where(r => r.Audience == ReportAudiences.Institution).ToList();

        Assert.NotEmpty(institution);
        Assert.DoesNotContain(institution, r => Forbidden.IsMatch(r.Title) || Forbidden.IsMatch(r.Question));
    }
}
