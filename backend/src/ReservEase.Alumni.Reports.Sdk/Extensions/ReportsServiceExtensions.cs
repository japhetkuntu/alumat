using Microsoft.Extensions.DependencyInjection;
using ReservEase.Alumni.Reports.Sdk.Services;

namespace ReservEase.Alumni.Reports.Sdk.Extensions;

public static class ReportsServiceExtensions
{
    /// <summary>
    /// Registers report requesting, listing and download for an API. Needs the Postgres SDK, the storage
    /// service and the Temporal client provider registered alongside it. Generation itself runs in
    /// Operations.Worker and isn't registered here.
    /// </summary>
    public static IServiceCollection AddReportJobs(this IServiceCollection services) =>
        services.AddScoped<IReportJobService, ReportJobService>();
}
