using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.Reports.Sdk.Models;

/// <summary>Names of the filters a report can take. A report ignores any it doesn't list in its definition.</summary>
public static class ReportParameters
{
    /// <summary>Inclusive start date, yyyy-MM-dd.</summary>
    public const string From = "from";
    /// <summary>Inclusive end date, yyyy-MM-dd.</summary>
    public const string To = "to";
    public const string Status = "status";
    public const string YearFrom = "yearFrom";
    public const string YearTo = "yearTo";
    public const string Profession = "profession";
    public const string Location = "location";
    /// <summary>The membership year a dues report is about. Defaults to the current year.</summary>
    public const string Year = "year";

    public static readonly IReadOnlyList<string> All = [From, To, Status, YearFrom, YearTo, Profession, Location, Year];
}

public static class ReportFormats
{
    public const string Xlsx = "xlsx";
    public const string Csv = "csv";
    public static readonly IReadOnlyList<string> All = [Xlsx, Csv];
}

public static class ReportTypes
{
    // Institution
    public const string Members = "members";
    public const string DuesStanding = "dues-standing";
    public const string Payments = "payments";
    public const string Fundraisers = "fundraisers";
    public const string EventAttendance = "event-attendance";
    public const string StoreOrders = "store-orders";
    public const string ServiceRequests = "service-requests";

    // Platform
    public const string PlatformInstitutions = "platform-institutions";
    public const string PlatformPayments = "platform-payments";
    public const string PlatformRevenue = "platform-revenue";
}

/// <param name="Key">One of <see cref="ReportTypes"/>.</param>
/// <param name="Question">What the report answers, in the reader's words. Shown under the title when choosing a report.</param>
/// <param name="Feature">The institution feature it belongs to, if any. A report for a switched-off feature isn't offered.</param>
/// <param name="UnscopedOnly">True when the data has no year-group or community of its own, so a scoped admin can't be shown a correct slice of it.</param>
/// <param name="Roles">Platform roles allowed to run it. Empty for institution reports, whose access is decided by Feature and UnscopedOnly.</param>
/// <param name="Parameters">The <see cref="ReportParameters"/> it understands.</param>
/// <param name="StatusOptions">Allowed values of the status filter, when it takes one.</param>
public record ReportDefinition(
    string Key, string Audience, string Title, string Question,
    string? Feature = null, bool UnscopedOnly = false,
    IReadOnlyList<string>? Roles = null,
    IReadOnlyList<string>? Parameters = null,
    IReadOnlyList<string>? StatusOptions = null)
{
    public IReadOnlyList<string> Roles { get; init; } = Roles ?? [];
    public IReadOnlyList<string> Parameters { get; init; } = Parameters ?? [];
    public IReadOnlyList<string> StatusOptions { get; init; } = StatusOptions ?? [];
}

/// <summary>
/// Every report the product can generate — the one list the APIs offer from, validate against, and
/// the worker builds from. Adding a report means adding it here and giving it a builder in
/// Operations.Worker (ReportDataBuilder); a key with no builder fails that job with a clear reason
/// rather than silently producing nothing.
/// </summary>
public static class ReportCatalog
{
    private static readonly string[] DateRange = [ReportParameters.From, ReportParameters.To];
    private static readonly string[] PaymentStatuses = ["Successful", "Pending", "Failed", "Rejected"];
    private static readonly string[] Finance = ["SuperAdmin", "Billing"];

    public static readonly IReadOnlyList<ReportDefinition> All =
    [
        new(ReportTypes.Members, ReportAudiences.Institution, "Member roster",
            "Who are our members, and how do we reach them?",
            Parameters: [ReportParameters.Status, ReportParameters.YearFrom, ReportParameters.YearTo, ReportParameters.Profession, ReportParameters.Location],
            StatusOptions: ["Active", "Pending", "Suspended", "Banned"]),
        new(ReportTypes.DuesStanding, ReportAudiences.Institution, "Dues standing",
            "Who has paid this year's dues, and who still owes?",
            Feature: InstitutionFeatures.Contributions,
            Parameters: [ReportParameters.Year, ReportParameters.Status],
            StatusOptions: ["Paid", "Owing"]),
        new(ReportTypes.Payments, ReportAudiences.Institution, "Payments ledger",
            "Every payment into a fundraiser or dues, with who paid, how much, and how.",
            Feature: InstitutionFeatures.Contributions,
            Parameters: [.. DateRange, ReportParameters.Status], StatusOptions: PaymentStatuses),
        new(ReportTypes.Fundraisers, ReportAudiences.Institution, "Fundraiser performance",
            "How is each fundraiser and dues period doing against its target?",
            Feature: InstitutionFeatures.Contributions),
        new(ReportTypes.EventAttendance, ReportAudiences.Institution, "Event attendance",
            "Who signed up for which event?",
            Feature: InstitutionFeatures.Events, Parameters: DateRange),
        new(ReportTypes.StoreOrders, ReportAudiences.Institution, "Store orders",
            "What was ordered, by whom, and where is each order in delivery?",
            Feature: InstitutionFeatures.Store, UnscopedOnly: true,
            Parameters: [.. DateRange, ReportParameters.Status], StatusOptions: ["Successful", "Pending", "Failed"]),
        new(ReportTypes.ServiceRequests, ReportAudiences.Institution, "Service requests",
            "Which services were requested, were they paid for, and what stage is each at?",
            Feature: InstitutionFeatures.Services, UnscopedOnly: true,
            Parameters: [.. DateRange, ReportParameters.Status], StatusOptions: ["Successful", "Pending", "Failed"]),

        new(ReportTypes.PlatformInstitutions, ReportAudiences.Platform, "Institutions overview",
            "Every institution: its size, how active it is, and what it has collected.",
            Roles: ["SuperAdmin", "Support", "Sales", "Billing"]),
        new(ReportTypes.PlatformPayments, ReportAudiences.Platform, "Payments ledger",
            "Every payment across every institution, with our fee and the gateway's on each.",
            Roles: Finance, Parameters: [.. DateRange, ReportParameters.Status], StatusOptions: PaymentStatuses),
        new(ReportTypes.PlatformRevenue, ReportAudiences.Platform, "Revenue by institution",
            "What each institution collected each month, and what we earned from it.",
            Roles: Finance, Parameters: DateRange),
    ];

    public static ReportDefinition? Find(string key) => All.FirstOrDefault(d => d.Key == key);
}
