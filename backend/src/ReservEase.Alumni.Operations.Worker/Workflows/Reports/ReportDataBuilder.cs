using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Reports.Sdk.Models;
using static ReservEase.Alumni.Operations.Worker.Workflows.Reports.ReportCellKind;

namespace ReservEase.Alumni.Operations.Worker.Workflows.Reports;

/// <summary>
/// What goes in each report: its columns and the query behind its rows. One method per key in
/// ReportCatalog.
///
/// The worker has no signed-in user and no ambient tenant, so nothing here leans on the global
/// tenant filter: every institution report filters on the job's InstitutionId itself, and applies
/// the requester's frozen scope (ReportJob.ScopeYearGroups / ScopeCommunityIds) the same way the
/// institution portal's own lists do. Reports are written for a person to read — names, not ids;
/// "Paid", not a status code — because the reader is a treasurer with a spreadsheet, not a developer.
/// </summary>
public class ReportDataBuilder(
    IAlumniPgRepository<Member> memberRepo,
    IAlumniPgRepository<Department> departmentRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<EventRsvp> rsvpRepo,
    IAlumniPgRepository<StoreOrder> storeOrderRepo,
    IAlumniPgRepository<ServiceRequest> serviceRequestRepo,
    IAlumniPgRepository<Institution> institutionRepo)
{
    private const int PageSize = 500;

    public ReportData Build(ReportJob job) => job.ReportType switch
    {
        ReportTypes.Members => Members(job),
        ReportTypes.DuesStanding => DuesStanding(job),
        ReportTypes.Payments => Payments(job),
        ReportTypes.Fundraisers => Fundraisers(job),
        ReportTypes.EventAttendance => EventAttendance(job),
        ReportTypes.StoreOrders => StoreOrders(job),
        ReportTypes.ServiceRequests => ServiceRequests(job),
        ReportTypes.PlatformInstitutions => PlatformInstitutions(),
        ReportTypes.PlatformPayments => PlatformPayments(job),
        ReportTypes.PlatformRevenue => PlatformRevenue(job),
        _ => throw new ReportNotSupportedException($"No builder for report type '{job.ReportType}'."),
    };

    // ── Institution reports ─────────────────────────────────────────────────

    private ReportData Members(ReportJob job) => new(
        [
            new("Member number"), new("First name"), new("Last name"), new("Email"), new("Phone"),
            new("Graduation year", Number), new("Department"), new("Programme"), new("Status"),
            new("Dues paid up"), new("Dues valid until", Date), new("Company"), new("Job title"), new("Location"),
            new("Joined", Date), new("Last signed in", Timestamp),
        ],
        MemberRows(job));

    private async IAsyncEnumerable<object?[]> MemberRows(ReportJob job, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var institutionId = job.InstitutionId!;
        var status = Param(job, ReportParameters.Status);
        var yearFrom = IntParam(job, ReportParameters.YearFrom);
        var yearTo = IntParam(job, ReportParameters.YearTo);
        var profession = Param(job, ReportParameters.Profession)?.ToLower();
        var location = Param(job, ReportParameters.Location)?.ToLower();

        var departments = await DepartmentNamesAsync(institutionId, ct);
        var members = (await ScopedMembersAsync(job, ct))
            .Where(m => (status == null || m.Status == status)
                && (yearFrom == null || m.GraduationYear >= yearFrom)
                && (yearTo == null || m.GraduationYear <= yearTo)
                && (profession == null || (m.JobTitle != null && m.JobTitle.ToLower().Contains(profession)))
                && (location == null || (m.Location != null && m.Location.ToLower().Contains(location))))
            .OrderBy(m => m.LastName).ThenBy(m => m.FirstName).ThenBy(m => m.Id);

        await foreach (var page in PagesAsync(members, ct))
            foreach (var m in page)
                yield return
                [
                    m.MemberNumber, m.FirstName, m.LastName, m.Email, m.Phone,
                    YearOrNull(m.GraduationYear), departments.GetValueOrDefault(m.DepartmentId), m.Program, m.Status,
                    m.IsMembershipActive, m.MembershipExpiry, m.Company, m.JobTitle, m.Location,
                    m.CreatedAt, m.LastLoginAt,
                ];
    }

    private ReportData DuesStanding(ReportJob job) => new(
        [
            new("Member number"), new("First name"), new("Last name"), new("Email"), new("Phone"),
            new("Graduation year", Number), new("Department"), new("Dues year", Number), new("Standing"),
            new("Amount paid", Money), new("Paid on", Date), new("Payment method"),
        ],
        DuesStandingRows(job));

    private async IAsyncEnumerable<object?[]> DuesStandingRows(ReportJob job, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var institutionId = job.InstitutionId!;
        var year = IntParam(job, ReportParameters.Year) ?? DateTime.UtcNow.Year;
        var standing = Param(job, ReportParameters.Status);

        var campaigns = await (await ScopedCampaignsAsync(job, ct))
            .Where(c => c.IsMembershipCampaign && c.MembershipYear == year).ToListAsync(ct);
        // No dues were set for that year, so nobody can be said to owe or to have paid.
        if (campaigns.Count == 0) yield break;

        var campaignIds = campaigns.Select(c => c.Id).ToList();
        var openToAll = campaigns.Any(c => c.YearGroups is null || c.YearGroups.Count == 0);
        var targetedYears = campaigns.SelectMany(c => c.YearGroups ?? []).ToHashSet();

        var payments = (await contributionRepo
                .GetQueryable(c => c.InstitutionId == institutionId && c.Status == "Successful" && campaignIds.Contains(c.CampaignId), ignoreQueryFilters: true)
                .AsNoTracking()
                .Select(c => new { c.MemberId, c.Amount, PaidAt = c.ConfirmedAt ?? c.CreatedAt, c.PaymentMethod })
                .ToListAsync(ct))
            .GroupBy(p => p.MemberId)
            .ToDictionary(g => g.Key, g => (Amount: g.Sum(p => p.Amount), g.OrderBy(p => p.PaidAt).Last().PaidAt, g.OrderBy(p => p.PaidAt).Last().PaymentMethod));

        var departments = await DepartmentNamesAsync(institutionId, ct);
        // Dues fall on approved members from the year they graduate — the same rule the member portal uses to decide who owes.
        var members = (await ScopedMembersAsync(job, ct))
            .Where(m => m.Status == "Active" && m.GraduationYear <= year)
            .OrderBy(m => m.LastName).ThenBy(m => m.FirstName).ThenBy(m => m.Id);

        await foreach (var page in PagesAsync(members, ct))
            foreach (var m in page)
            {
                if (!openToAll && !targetedYears.Contains(m.GraduationYear)) continue;
                var paid = payments.TryGetValue(m.Id, out var payment);
                if (standing is not null && standing != (paid ? "Paid" : "Owing")) continue;
                yield return
                [
                    m.MemberNumber, m.FirstName, m.LastName, m.Email, m.Phone,
                    YearOrNull(m.GraduationYear), departments.GetValueOrDefault(m.DepartmentId), year, paid ? "Paid" : "Owing",
                    paid ? payment.Amount : null, paid ? payment.PaidAt : null, paid ? payment.PaymentMethod : null,
                ];
            }
    }

    private ReportData Payments(ReportJob job) => new(
        [
            new("Date", Timestamp), new("Paid by"), new("Email"), new("Paid towards"), new("Kind"),
            new("Amount", Money), new("Method"), new("Status"), new("Reference"), new("Paid as guest"),
        ],
        PaymentRows(job));

    private async IAsyncEnumerable<object?[]> PaymentRows(ReportJob job, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var institutionId = job.InstitutionId!;
        var (from, to) = DateRange(job);
        var status = Param(job, ReportParameters.Status);

        var campaigns = await (await ScopedCampaignsAsync(job, ct))
            .Select(c => new { c.Id, c.Title, c.IsMembershipCampaign }).ToDictionaryAsync(c => c.Id, ct);
        var scopedCampaignIds = job.RequesterIsScoped ? campaigns.Keys.ToList() : null;

        var query = contributionRepo
            .GetQueryable(c => c.InstitutionId == institutionId
                && (scopedCampaignIds == null || scopedCampaignIds.Contains(c.CampaignId))
                && (status == null || c.Status == status)
                && (from == null || (c.ConfirmedAt ?? c.CreatedAt) >= from)
                && (to == null || (c.ConfirmedAt ?? c.CreatedAt) < to), ignoreQueryFilters: true)
            .AsNoTracking()
            .OrderByDescending(c => c.ConfirmedAt ?? c.CreatedAt).ThenBy(c => c.Id);

        await foreach (var page in PagesAsync(query, ct))
        {
            var people = await PeopleAsync(page.Select(c => c.MemberId), ct);
            foreach (var c in page)
            {
                var campaign = campaigns.GetValueOrDefault(c.CampaignId);
                var (name, email) = Person(people, c.MemberId, c.Member);
                yield return
                [
                    c.ConfirmedAt ?? c.CreatedAt, name, email, campaign?.Title ?? c.Campaign?.Title,
                    campaign?.IsMembershipCampaign == true ? "Dues" : "Fundraiser",
                    c.Amount, c.PaymentMethod, c.Status, c.TransactionRef, c.IsGuestPayment,
                ];
            }
        }
    }

    private ReportData Fundraisers(ReportJob job) => new(
        [
            new("Title"), new("Kind"), new("Status"), new("Target", Money), new("Collected", Money), new("Percent of target", Number),
            new("People who paid", Number), new("Amount per member", Money), new("Deadline", Date), new("Open to"), new("Created", Date),
        ],
        FundraiserRows(job));

    private async IAsyncEnumerable<object?[]> FundraiserRows(ReportJob job, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var campaigns = (await ScopedCampaignsAsync(job, ct)).OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id);
        await foreach (var page in PagesAsync(campaigns, ct))
            foreach (var c in page)
                yield return
                [
                    c.Title, c.IsMembershipCampaign ? $"Dues {c.MembershipYear}" : "Fundraiser", c.Status.ToString(),
                    c.TargetAmount, c.CollectedAmount, c.TargetAmount > 0 ? Math.Round(c.CollectedAmount / c.TargetAmount * 100, 1) : null,
                    c.PaidCount, c.AmountPerMember, c.Deadline,
                    c.YearGroups is { Count: > 0 } ? string.Join(", ", c.YearGroups.Order()) : "Everyone", c.CreatedAt,
                ];
    }

    private ReportData EventAttendance(ReportJob job) => new(
        [
            new("Event"), new("Event date", Timestamp), new("Venue"), new("First name"), new("Last name"), new("Email"),
            new("Graduation year", Number), new("Sign-up status"), new("Signed up on", Timestamp),
        ],
        EventAttendanceRows(job));

    private async IAsyncEnumerable<object?[]> EventAttendanceRows(ReportJob job, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var institutionId = job.InstitutionId!;
        var (from, to) = DateRange(job);
        var scoped = job.RequesterIsScoped;
        var years = job.ScopeYearGroups;
        var communities = job.ScopeCommunityIds;
        var requesterId = job.RequestedById;

        var events = await eventRepo
            .GetQueryable(e => e.InstitutionId == institutionId
                && (!scoped || e.CreatedBy == requesterId
                    || (e.YearGroups != null && e.YearGroups.Any(y => years.Contains(y)))
                    || (e.CommunityId != null && communities.Contains(e.CommunityId)))
                && (from == null || e.StartDate >= from) && (to == null || e.StartDate < to), ignoreQueryFilters: true)
            .AsNoTracking().OrderByDescending(e => e.StartDate).ThenBy(e => e.Id)
            .Select(e => new { e.Id, e.Title, e.StartDate, e.Venue }).ToListAsync(ct);

        foreach (var ev in events)
        {
            var rsvps = rsvpRepo.GetQueryable(r => r.InstitutionId == institutionId && r.EventId == ev.Id, ignoreQueryFilters: true)
                .AsNoTracking().OrderBy(r => r.CreatedAt).ThenBy(r => r.Id);
            await foreach (var page in PagesAsync(rsvps, ct))
            {
                var people = await PeopleAsync(page.Select(r => r.MemberId), ct);
                foreach (var r in page)
                {
                    people.TryGetValue(r.MemberId, out var m);
                    yield return
                    [
                        ev.Title, ev.StartDate, ev.Venue, m?.FirstName, m?.LastName, m?.Email,
                        m is null ? null : YearOrNull(m.GraduationYear), r.Status, r.CreatedAt,
                    ];
                }
            }
        }
    }

    private ReportData StoreOrders(ReportJob job) => new(
        [
            new("Order number"), new("Date", Timestamp), new("Ordered by"), new("Email"), new("Product"), new("Options"),
            new("Quantity", Number), new("Unit price", Money), new("Line total", Money), new("Order total", Money),
            new("Payment"), new("Delivery"), new("Reference"),
        ],
        StoreOrderRows(job));

    private async IAsyncEnumerable<object?[]> StoreOrderRows(ReportJob job, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var institutionId = job.InstitutionId!;
        var (from, to) = DateRange(job);
        var status = Param(job, ReportParameters.Status);

        var orders = storeOrderRepo
            .GetQueryable(o => o.InstitutionId == institutionId
                && (status == null || o.Status == status)
                && (from == null || (o.ConfirmedAt ?? o.CreatedAt) >= from)
                && (to == null || (o.ConfirmedAt ?? o.CreatedAt) < to), ignoreQueryFilters: true)
            .AsNoTracking().OrderByDescending(o => o.ConfirmedAt ?? o.CreatedAt).ThenBy(o => o.Id);

        await foreach (var page in PagesAsync(orders, ct))
        {
            var people = await PeopleAsync(page.Select(o => o.MemberId), ct);
            foreach (var o in page)
            {
                var (name, email) = Person(people, o.MemberId, o.Member);
                // One row per product on the order, so quantities and line totals can be summed by product.
                foreach (var item in o.Items)
                    yield return
                    [
                        o.OrderNumber, o.ConfirmedAt ?? o.CreatedAt, name, email, item.ProductName,
                        item.VariantOptions is { Count: > 0 } ? string.Join(", ", item.VariantOptions.Select(v => $"{v.Key}: {v.Value}")) : null,
                        item.Quantity, item.UnitPrice, item.UnitPrice * item.Quantity, o.TotalAmount,
                        o.Status, o.DeliveryStatus, o.TransactionRef,
                    ];
            }
        }
    }

    private ReportData ServiceRequests(ReportJob job) => new(
        [
            new("Request number"), new("Date", Timestamp), new("Requested by"), new("Email"), new("Service"),
            new("Amount", Money), new("Payment"), new("Stage"), new("Reference"),
        ],
        ServiceRequestRows(job));

    private async IAsyncEnumerable<object?[]> ServiceRequestRows(ReportJob job, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var institutionId = job.InstitutionId!;
        var (from, to) = DateRange(job);
        var status = Param(job, ReportParameters.Status);

        var requests = serviceRequestRepo
            .GetQueryable(r => r.InstitutionId == institutionId
                && (status == null || r.PaymentStatus == status)
                && (from == null || (r.ConfirmedAt ?? r.CreatedAt) >= from)
                && (to == null || (r.ConfirmedAt ?? r.CreatedAt) < to), ignoreQueryFilters: true)
            .AsNoTracking().OrderByDescending(r => r.ConfirmedAt ?? r.CreatedAt).ThenBy(r => r.Id);

        await foreach (var page in PagesAsync(requests, ct))
        {
            var people = await PeopleAsync(page.Select(r => r.MemberId), ct);
            foreach (var r in page)
            {
                var (name, email) = Person(people, r.MemberId, r.Member);
                yield return [r.RequestNumber, r.ConfirmedAt ?? r.CreatedAt, name, email, r.ServiceTypeName, r.Amount, r.PaymentStatus, r.CurrentStage, r.TransactionRef];
            }
        }
    }

    // ── Platform reports ────────────────────────────────────────────────────

    private ReportData PlatformInstitutions() => new(
        [
            new("Institution"), new("Address"), new("Kind"), new("Status"), new("Onboarded", Date), new("Activated", Date),
            new("Members", Number), new("Approved members", Number), new("Signed in, last 30 days", Number),
            new("Collected, all time", Money), new("Our earnings, all time", Money), new("Contact"), new("Contact email"),
        ],
        PlatformInstitutionRows());

    private async IAsyncEnumerable<object?[]> PlatformInstitutionRows([EnumeratorCancellation] CancellationToken ct = default)
    {
        var recently = DateTime.UtcNow.AddDays(-30);
        var members = await memberRepo.GetQueryable(null, ignoreQueryFilters: true).AsNoTracking()
            .GroupBy(m => m.InstitutionId)
            .Select(g => new { g.Key, Total = g.Count(), Active = g.Count(m => m.Status == "Active"), Recent = g.Count(m => m.LastLoginAt >= recently) })
            .ToDictionaryAsync(x => x.Key, ct);
        var money = await MoneyByInstitutionAsync(null, null, ct);

        var institutions = institutionRepo.GetQueryable(null, ignoreQueryFilters: true).AsNoTracking().OrderBy(i => i.Name).ThenBy(i => i.Id);
        await foreach (var page in PagesAsync(institutions, ct))
            foreach (var i in page)
            {
                members.TryGetValue(i.Id, out var m);
                var totals = money.Where(x => x.InstitutionId == i.Id).ToList();
                yield return
                [
                    i.Name, i.Slug, i.OrganizationType, i.Status, i.OnboardedAt, i.ActivatedAt,
                    m?.Total ?? 0, m?.Active ?? 0, m?.Recent ?? 0,
                    totals.Sum(x => x.Collected), totals.Sum(x => x.Earned), i.ContactName, i.ContactEmail,
                ];
            }
    }

    private ReportData PlatformPayments(ReportJob job) => new(
        [
            new("Date", Timestamp), new("Institution"), new("Source"), new("Paid by"), new("Email"), new("For"),
            new("Amount", Money), new("Our earnings", Money), new("Gateway fee", Money), new("Charged to payer", Money),
            new("Method"), new("Status"), new("Reference"),
        ],
        PlatformPaymentRows(job));

    private async IAsyncEnumerable<object?[]> PlatformPaymentRows(ReportJob job, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (from, to) = DateRange(job);
        var status = Param(job, ReportParameters.Status);
        var institutions = await institutionRepo.GetQueryable(null, ignoreQueryFilters: true).AsNoTracking()
            .Select(i => new { i.Id, i.Name }).ToDictionaryAsync(i => i.Id, i => i.Name, ct);

        // One source after another, each newest first — the Source column says which block a row is in.
        var contributions = contributionRepo
            .GetQueryable(c => (status == null || c.Status == status)
                && (from == null || (c.ConfirmedAt ?? c.CreatedAt) >= from) && (to == null || (c.ConfirmedAt ?? c.CreatedAt) < to), ignoreQueryFilters: true)
            .AsNoTracking().OrderByDescending(c => c.ConfirmedAt ?? c.CreatedAt).ThenBy(c => c.Id);
        await foreach (var page in PagesAsync(contributions, ct))
            foreach (var c in page)
                yield return
                [
                    c.ConfirmedAt ?? c.CreatedAt, institutions.GetValueOrDefault(c.InstitutionId), "Contribution", SnapshotName(c.Member), c.Member?.Email, c.Campaign?.Title,
                    c.Amount, c.PlatformFeeAmount + c.PlatformRevenueAmount, c.GatewayFeeAmount, c.GrossChargeAmount, c.PaymentMethod, c.Status, c.TransactionRef,
                ];

        var orders = storeOrderRepo
            .GetQueryable(o => (status == null || o.Status == status)
                && (from == null || (o.ConfirmedAt ?? o.CreatedAt) >= from) && (to == null || (o.ConfirmedAt ?? o.CreatedAt) < to), ignoreQueryFilters: true)
            .AsNoTracking().OrderByDescending(o => o.ConfirmedAt ?? o.CreatedAt).ThenBy(o => o.Id);
        await foreach (var page in PagesAsync(orders, ct))
            foreach (var o in page)
                yield return
                [
                    o.ConfirmedAt ?? o.CreatedAt, institutions.GetValueOrDefault(o.InstitutionId), "Store order", SnapshotName(o.Member), o.Member?.Email, $"Order {o.OrderNumber}",
                    o.TotalAmount, o.PlatformFeeAmount, o.GatewayFeeAmount, o.GrossChargeAmount, o.PaymentMethod, o.Status, o.TransactionRef,
                ];

        var requests = serviceRequestRepo
            .GetQueryable(r => (status == null || r.PaymentStatus == status)
                && (from == null || (r.ConfirmedAt ?? r.CreatedAt) >= from) && (to == null || (r.ConfirmedAt ?? r.CreatedAt) < to), ignoreQueryFilters: true)
            .AsNoTracking().OrderByDescending(r => r.ConfirmedAt ?? r.CreatedAt).ThenBy(r => r.Id);
        await foreach (var page in PagesAsync(requests, ct))
            foreach (var r in page)
                yield return
                [
                    r.ConfirmedAt ?? r.CreatedAt, institutions.GetValueOrDefault(r.InstitutionId), "Service request", SnapshotName(r.Member), r.Member?.Email, r.ServiceTypeName,
                    r.Amount, r.PlatformFeeAmount, r.GatewayFeeAmount, r.GrossChargeAmount, r.PaymentMethod, r.PaymentStatus, r.TransactionRef,
                ];
    }

    private ReportData PlatformRevenue(ReportJob job) => new(
        [
            new("Institution"), new("Month"), new("Contributions", Money), new("Store", Money), new("Services", Money),
            new("Collected", Money), new("Our earnings", Money), new("Payments", Number),
        ],
        PlatformRevenueRows(job));

    private async IAsyncEnumerable<object?[]> PlatformRevenueRows(ReportJob job, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (from, to) = DateRange(job);
        // With no range given, the last twelve full months and this one.
        from ??= new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-12);

        var institutions = await institutionRepo.GetQueryable(null, ignoreQueryFilters: true).AsNoTracking()
            .Select(i => new { i.Id, i.Name }).ToDictionaryAsync(i => i.Id, i => i.Name, ct);
        var money = await MoneyByInstitutionAsync(from, to, ct);

        var rows = money
            .GroupBy(x => (x.InstitutionId, x.Year, x.Month))
            .Select(g => new
            {
                Name = institutions.GetValueOrDefault(g.Key.InstitutionId) ?? "Unknown institution",
                g.Key.Year, g.Key.Month,
                Contributions = g.Where(x => x.Source == "Contribution").Sum(x => x.Collected),
                Store = g.Where(x => x.Source == "Store").Sum(x => x.Collected),
                Services = g.Where(x => x.Source == "Service").Sum(x => x.Collected),
                Earned = g.Sum(x => x.Earned),
                Payments = g.Sum(x => x.Payments),
            })
            .OrderBy(r => r.Name).ThenBy(r => r.Year).ThenBy(r => r.Month);

        foreach (var r in rows)
            yield return
            [
                r.Name, new DateTime(r.Year, r.Month, 1).ToString("MMM yyyy", CultureInfo.InvariantCulture),
                r.Contributions, r.Store, r.Services, r.Contributions + r.Store + r.Services, r.Earned, r.Payments,
            ];
    }

    private record MoneyRow(string InstitutionId, int Year, int Month, string Source, decimal Collected, decimal Earned, int Payments);

    /// <summary>Successful payments from all three sources, summed per institution per month. Small however much was paid: one row per institution, month and source.</summary>
    private async Task<List<MoneyRow>> MoneyByInstitutionAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var contributions = await contributionRepo
            .GetQueryable(c => c.Status == "Successful"
                && (from == null || (c.ConfirmedAt ?? c.CreatedAt) >= from) && (to == null || (c.ConfirmedAt ?? c.CreatedAt) < to), ignoreQueryFilters: true)
            .GroupBy(c => new { c.InstitutionId, (c.ConfirmedAt ?? c.CreatedAt).Year, (c.ConfirmedAt ?? c.CreatedAt).Month })
            .Select(g => new { g.Key.InstitutionId, g.Key.Year, g.Key.Month, Collected = g.Sum(c => c.Amount), Earned = g.Sum(c => c.PlatformFeeAmount + c.PlatformRevenueAmount), Payments = g.Count() })
            .ToListAsync(ct);
        var orders = await storeOrderRepo
            .GetQueryable(o => o.Status == "Successful"
                && (from == null || (o.ConfirmedAt ?? o.CreatedAt) >= from) && (to == null || (o.ConfirmedAt ?? o.CreatedAt) < to), ignoreQueryFilters: true)
            .GroupBy(o => new { o.InstitutionId, (o.ConfirmedAt ?? o.CreatedAt).Year, (o.ConfirmedAt ?? o.CreatedAt).Month })
            .Select(g => new { g.Key.InstitutionId, g.Key.Year, g.Key.Month, Collected = g.Sum(o => o.TotalAmount), Earned = g.Sum(o => o.PlatformFeeAmount), Payments = g.Count() })
            .ToListAsync(ct);
        var requests = await serviceRequestRepo
            .GetQueryable(r => r.PaymentStatus == "Successful"
                && (from == null || (r.ConfirmedAt ?? r.CreatedAt) >= from) && (to == null || (r.ConfirmedAt ?? r.CreatedAt) < to), ignoreQueryFilters: true)
            .GroupBy(r => new { r.InstitutionId, (r.ConfirmedAt ?? r.CreatedAt).Year, (r.ConfirmedAt ?? r.CreatedAt).Month })
            .Select(g => new { g.Key.InstitutionId, g.Key.Year, g.Key.Month, Collected = g.Sum(r => r.Amount), Earned = g.Sum(r => r.PlatformFeeAmount), Payments = g.Count() })
            .ToListAsync(ct);

        return
        [
            .. contributions.Select(x => new MoneyRow(x.InstitutionId, x.Year, x.Month, "Contribution", x.Collected, x.Earned, x.Payments)),
            .. orders.Select(x => new MoneyRow(x.InstitutionId, x.Year, x.Month, "Store", x.Collected, x.Earned, x.Payments)),
            .. requests.Select(x => new MoneyRow(x.InstitutionId, x.Year, x.Month, "Service", x.Collected, x.Earned, x.Payments)),
        ];
    }

    // ── Shared ──────────────────────────────────────────────────────────────

    /// <summary>The institution's members the requester may see: everyone for a SuperAdmin; their year groups and their communities' approved members for a ScopedAdmin.</summary>
    private async Task<IQueryable<Member>> ScopedMembersAsync(ReportJob job, CancellationToken ct)
    {
        var institutionId = job.InstitutionId!;
        if (!job.RequesterIsScoped)
            return memberRepo.GetQueryable(m => m.InstitutionId == institutionId, ignoreQueryFilters: true).AsNoTracking();

        var years = job.ScopeYearGroups;
        var communities = job.ScopeCommunityIds;
        var communityMemberIds = communities.Count == 0
            ? []
            : await membershipRepo.GetQueryable(m => m.InstitutionId == institutionId && communities.Contains(m.CommunityId) && m.Status == "Approved", ignoreQueryFilters: true)
                .Select(m => m.MemberId).Distinct().ToListAsync(ct);
        return memberRepo.GetQueryable(m => m.InstitutionId == institutionId
            && (years.Contains(m.GraduationYear) || communityMemberIds.Contains(m.Id)), ignoreQueryFilters: true).AsNoTracking();
    }

    /// <summary>Async only for symmetry with <see cref="ScopedMembersAsync"/>; campaigns carry their own year groups and community, so no lookup is needed.</summary>
    private Task<IQueryable<Campaign>> ScopedCampaignsAsync(ReportJob job, CancellationToken ct)
    {
        var institutionId = job.InstitutionId!;
        var scoped = job.RequesterIsScoped;
        var years = job.ScopeYearGroups;
        var communities = job.ScopeCommunityIds;
        var requesterId = job.RequestedById;
        return Task.FromResult(campaignRepo.GetQueryable(c => c.InstitutionId == institutionId
            && (!scoped || c.CreatedBy == requesterId
                || (c.YearGroups != null && c.YearGroups.Any(y => years.Contains(y)))
                || (c.CommunityId != null && communities.Contains(c.CommunityId))), ignoreQueryFilters: true).AsNoTracking());
    }

    private async Task<Dictionary<string, string>> DepartmentNamesAsync(string institutionId, CancellationToken ct) =>
        await departmentRepo.GetQueryable(d => d.InstitutionId == institutionId, ignoreQueryFilters: true).AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

    /// <summary>The live member rows behind a page of records, so a name changed since the record was written reads as it is now.</summary>
    private async Task<Dictionary<string, Member>> PeopleAsync(IEnumerable<string> memberIds, CancellationToken ct)
    {
        var ids = memberIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        if (ids.Count == 0) return [];
        return await memberRepo.GetQueryable(m => ids.Contains(m.Id), ignoreQueryFilters: true).AsNoTracking().ToDictionaryAsync(m => m.Id, ct);
    }

    /// <summary>The live member if there still is one, else the snapshot frozen on the record (a guest payer, or a member since deleted).</summary>
    private static (string? Name, string? Email) Person(Dictionary<string, Member> people, string memberId, MemberSnapshot? snapshot) =>
        people.TryGetValue(memberId, out var m)
            ? ($"{m.FirstName} {m.LastName}".Trim(), m.Email)
            : (SnapshotName(snapshot), snapshot?.Email);

    private static string? SnapshotName(MemberSnapshot? snapshot) =>
        snapshot is null ? null : $"{snapshot.FirstName} {snapshot.LastName}".Trim();

    /// <summary>Community-type institutions don't collect a graduation year; 0 is "none", and reads better as an empty cell.</summary>
    private static int? YearOrNull(int year) => year > 0 ? year : null;

    private static string? Param(ReportJob job, string key) =>
        job.Parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static int? IntParam(ReportJob job, string key) =>
        int.TryParse(Param(job, key), out var value) ? value : null;

    /// <summary>The chosen dates as a half-open UTC range: from the start of the first day up to, not including, the day after the last.</summary>
    private static (DateTime? From, DateTime? To) DateRange(ReportJob job)
    {
        static DateTime? Day(string? value) =>
            DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var day) ? day : null;
        return (Day(Param(job, ReportParameters.From)), Day(Param(job, ReportParameters.To))?.AddDays(1));
    }

    /// <summary>
    /// Reads an ordered query a page at a time. The ordering must end in a unique column (every caller
    /// ends on Id) so that pages neither overlap nor skip a row.
    /// </summary>
    private static async IAsyncEnumerable<List<T>> PagesAsync<T>(IQueryable<T> ordered, [EnumeratorCancellation] CancellationToken ct)
    {
        for (var skip = 0; ; skip += PageSize)
        {
            var page = await ordered.Skip(skip).Take(PageSize).ToListAsync(ct);
            if (page.Count == 0) yield break;
            yield return page;
            if (page.Count < PageSize) yield break;
        }
    }
}
