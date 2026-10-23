using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using Microsoft.EntityFrameworkCore;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

/// <summary>
/// Estimates what Paystack will settle to this institution's own bank
/// account — see <see cref="PayoutWindowCalculator"/> for the settlement-window
/// model and why this is computed from our own confirmed transactions rather
/// than a Paystack Settlement API call.
/// </summary>
public class PayoutService(
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<StoreOrder> storeOrderRepo,
    IAlumniPgRepository<ServiceRequest> serviceRequestRepo,
    IAlumniPgRepository<EventRsvp> eventRsvpRepo,
    IAlumniPgRepository<AlumniEvent> eventRepo,
    IAlumniPgRepository<ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member> memberRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Batch> batchRepo,
    IAlumniPgRepository<InstitutionEntity> institutionRepo,
    ICurrentTenantService currentTenant,
    ILogger<PayoutService> logger) : IPayoutService
{
    public async Task<IApiResponse<PayoutForecastResponse>> GetForecastAsync()
    {
        try
        {
            var institution = await institutionRepo.GetOneAsync(i => i.Id == currentTenant.InstitutionId);
            var payoutsConfigured = !string.IsNullOrWhiteSpace(institution?.PaystackSubaccountCode);

            var windows = PayoutWindowCalculator.GetWindows(DateTime.UtcNow);
            var lastStart = windows.LastWindowStart; var lastEnd = windows.LastWindowEnd;
            var nextStart = windows.NextWindowStart; var nextEnd = windows.NextWindowEnd;

            // Totals are worked out in the database and only the sums come back, one small query per kind of payment for both
            // windows together, instead of loading every payment row (with its snapshots) just to add them up. A payment is
            // told apart by when it was confirmed: before nextStart is the last window, from it on is the next one.
            var contributionTotals = await contributionRepo.GetQueryable(c =>
                    c.Status == "Successful" && c.PaymentMethod == "Paystack" && c.ConfirmedAt != null
                    && ((c.ConfirmedAt >= lastStart && c.ConfirmedAt < lastEnd) || (c.ConfirmedAt >= nextStart && c.ConfirmedAt < nextEnd)))
                .GroupBy(c => new { c.CampaignId, IsNext = c.ConfirmedAt >= nextStart })
                .Select(g => new { g.Key.CampaignId, g.Key.IsNext, Amount = g.Sum(c => c.Amount), Count = g.Count() })
                .ToListAsync();
            var batchSettled = (await ResolveAccountsAsync(contributionTotals.Select(c => c.CampaignId).Distinct().ToList())).Where(kv => kv.Value is not null).Select(kv => kv.Key).ToHashSet();

            var orderTotals = await storeOrderRepo.GetQueryable(o =>
                    o.Status == "Successful" && o.PaymentMethod == "Paystack" && o.ConfirmedAt != null
                    && ((o.ConfirmedAt >= lastStart && o.ConfirmedAt < lastEnd) || (o.ConfirmedAt >= nextStart && o.ConfirmedAt < nextEnd)))
                .GroupBy(o => o.ConfirmedAt >= nextStart)
                .Select(g => new { IsNext = g.Key, Amount = g.Sum(o => o.TotalAmount), Count = g.Count() })
                .ToListAsync();

            var requestTotals = await serviceRequestRepo.GetQueryable(r =>
                    r.PaymentStatus == "Successful" && r.PaymentMethod == "Paystack" && r.ConfirmedAt != null
                    && ((r.ConfirmedAt >= lastStart && r.ConfirmedAt < lastEnd) || (r.ConfirmedAt >= nextStart && r.ConfirmedAt < nextEnd)))
                .GroupBy(r => r.ConfirmedAt >= nextStart)
                .Select(g => new { IsNext = g.Key, Amount = g.Sum(r => r.Amount), Count = g.Count() })
                .ToListAsync();

            // Event tickets settle to the institution's own account like store orders and service requests do.
            var ticketTotals = await eventRsvpRepo.GetQueryable(EventTicketRules.SettledInEither(lastStart, lastEnd, nextStart, nextEnd))
                .GroupBy(t => t.PaidAt >= nextStart)
                .Select(g => new { IsNext = g.Key, Amount = g.Sum(t => t.AmountPaid ?? 0m), Count = g.Count() })
                .ToListAsync();

            PayoutWindowDto Window(DateOnly date, bool next)
            {
                var amount = contributionTotals.Where(c => c.IsNext == next && !batchSettled.Contains(c.CampaignId)).Sum(c => c.Amount)
                    + orderTotals.Where(o => o.IsNext == next).Sum(o => o.Amount)
                    + requestTotals.Where(r => r.IsNext == next).Sum(r => r.Amount)
                    + ticketTotals.Where(t => t.IsNext == next).Sum(t => t.Amount);
                var count = contributionTotals.Where(c => c.IsNext == next && !batchSettled.Contains(c.CampaignId)).Sum(c => c.Count)
                    + orderTotals.Where(o => o.IsNext == next).Sum(o => o.Count)
                    + requestTotals.Where(r => r.IsNext == next).Sum(r => r.Count)
                    + ticketTotals.Where(t => t.IsNext == next).Sum(t => t.Count);
                return new PayoutWindowDto(date, amount, count);
            }

            return new PayoutForecastResponse(payoutsConfigured, Window(windows.LastPayoutDate, false), Window(windows.NextPayoutDate, true)).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "GetForecastAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<PayoutForecastResponse>("Failed to getforecast");
        }
    }

    public async Task<IApiResponse<PayoutHistoryResponse>> GetHistoryAsync(DateOnly? to, int days, string? account)
    {
        try
        {
            days = Math.Clamp(days, 1, 366);
            var institution = await institutionRepo.GetOneAsync(i => i.Id == currentTenant.InstitutionId);
            var payoutsConfigured = !string.IsNullOrWhiteSpace(institution?.PaystackSubaccountCode);

            // Only payouts that have already happened: the latest is the most recent working morning, never an upcoming one.
            var latest = PayoutWindowCalculator.GetWindows(DateTime.UtcNow).LastPayoutDate;
            var end = PayoutWindowCalculator.PreviousWorkingDayOnOrBefore(to is null || to.Value > latest ? latest : to.Value);
            var from = end.AddDays(-(days - 1));
            // A payment confirmed from this instant on is paid out on `from` or later, and one confirmed before `end` is paid out by `end`.
            var start = PayoutWindowCalculator.WindowFor(from).Start;
            var endUtc = end.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            // Sums per kind of payment and per calendar day, worked out in the database; the day each one is paid out on is
            // then a small calculation in memory over those few rows.
            var contributionDays = await contributionRepo.GetQueryable(c =>
                    c.Status == "Successful" && c.PaymentMethod == "Paystack" && c.ConfirmedAt != null && c.ConfirmedAt >= start && c.ConfirmedAt < endUtc)
                .GroupBy(c => new { c.CampaignId, Day = c.ConfirmedAt!.Value.Date })
                .Select(g => new { g.Key.CampaignId, g.Key.Day, Amount = g.Sum(c => c.Amount), Count = g.Count() })
                .ToListAsync();
            var campaignIds = contributionDays.Select(c => c.CampaignId).Distinct().ToList();
            var accountOf = await ResolveAccountsAsync(campaignIds);
            var membershipIds = campaignIds.Count == 0 ? [] : (await campaignRepo.GetQueryable(c => campaignIds.Contains(c.Id) && c.IsMembershipCampaign)
                .Select(c => c.Id).ToListAsync()).ToHashSet();

            var orderDays = await storeOrderRepo.GetQueryable(o =>
                    o.Status == "Successful" && o.PaymentMethod == "Paystack" && o.ConfirmedAt != null && o.ConfirmedAt >= start && o.ConfirmedAt < endUtc)
                .GroupBy(o => o.ConfirmedAt!.Value.Date)
                .Select(g => new { Day = g.Key, Amount = g.Sum(o => o.TotalAmount), Count = g.Count() })
                .ToListAsync();
            var requestDays = await serviceRequestRepo.GetQueryable(r =>
                    r.PaymentStatus == "Successful" && r.PaymentMethod == "Paystack" && r.ConfirmedAt != null && r.ConfirmedAt >= start && r.ConfirmedAt < endUtc)
                .GroupBy(r => r.ConfirmedAt!.Value.Date)
                .Select(g => new { Day = g.Key, Amount = g.Sum(r => r.Amount), Count = g.Count() })
                .ToListAsync();
            var ticketDays = await eventRsvpRepo.GetQueryable(EventTicketRules.SettledBetween(start, endUtc))
                .GroupBy(t => t.PaidAt!.Value.Date)
                .Select(g => new { Day = g.Key, Amount = g.Sum(t => t.AmountPaid ?? 0m), Count = g.Count() })
                .ToListAsync();

            var accounts = await AccountOptionsAsync(institution);
            account = NormaliseAccount(account, accounts);
            var accountNames = accounts.ToDictionary(a => a.Key, a => a.Name);

            // Every payment is filed under the day it is paid out on and the account it is paid into, so the same morning can
            // have several payouts (the main account plus year groups with their own).
            var byDay = new Dictionary<DateOnly, Dictionary<(string Account, string Source), (decimal Amount, int Count)>>();
            void Add(DateTime day, string accountKey, string source, decimal amount, int count)
            {
                if (account is not null && accountKey != account) return;
                var payout = PayoutWindowCalculator.NextWorkingDayAfter(DateOnly.FromDateTime(day));
                if (payout < from || payout > end) return;
                if (!byDay.TryGetValue(payout, out var parts)) byDay[payout] = parts = [];
                parts.TryGetValue((accountKey, source), out var cur);
                parts[(accountKey, source)] = (cur.Amount + amount, cur.Count + count);
            }
            foreach (var c in contributionDays)
                Add(c.Day, accountOf.GetValueOrDefault(c.CampaignId) ?? MainAccount, membershipIds.Contains(c.CampaignId) ? PayoutSources.Dues : PayoutSources.Fundraisers, c.Amount, c.Count);
            foreach (var o in orderDays) Add(o.Day, MainAccount, PayoutSources.Store, o.Amount, o.Count);
            foreach (var r in requestDays) Add(r.Day, MainAccount, PayoutSources.Services, r.Amount, r.Count);
            foreach (var t in ticketDays) Add(t.Day, MainAccount, PayoutSources.EventRsvps, t.Amount, t.Count);

            var result = byDay
                .Where(d => d.Value.Values.Sum(v => v.Amount) > 0)
                .OrderByDescending(d => d.Key)
                .Select(d => new PayoutDayDto(
                    d.Key, d.Value.Values.Sum(v => v.Amount), d.Value.Values.Sum(v => v.Count),
                    d.Value.GroupBy(v => v.Key.Source)
                        .Select(g => new PayoutSourceTotalDto(g.Key, g.Sum(v => v.Value.Amount), g.Sum(v => v.Value.Count)))
                        .OrderByDescending(v => v.Amount).ToList(),
                    d.Value.GroupBy(v => v.Key.Account)
                        .Select(g => new PayoutAccountTotalDto(g.Key, accountNames.GetValueOrDefault(g.Key) ?? "Year group account", g.Sum(v => v.Value.Amount), g.Sum(v => v.Value.Count)))
                        .OrderByDescending(v => v.Key == MainAccount).ThenByDescending(v => v.Amount).ToList()))
                .ToList();

            // Anything confirmed before this range would be paid out earlier than it, so there is more to see by going back.
            var hasOlder =
                await contributionRepo.GetQueryable(c => c.Status == "Successful" && c.PaymentMethod == "Paystack" && c.ConfirmedAt != null && c.ConfirmedAt < start).AnyAsync()
                || await storeOrderRepo.GetQueryable(o => o.Status == "Successful" && o.PaymentMethod == "Paystack" && o.ConfirmedAt != null && o.ConfirmedAt < start).AnyAsync()
                || await serviceRequestRepo.GetQueryable(r => r.PaymentStatus == "Successful" && r.PaymentMethod == "Paystack" && r.ConfirmedAt != null && r.ConfirmedAt < start).AnyAsync()
                || await eventRsvpRepo.GetQueryable(EventTicketRules.SettledBetween(DateTime.UnixEpoch, start)).AnyAsync();

            return new PayoutHistoryResponse(payoutsConfigured, accounts, account ?? "all", result, result.Sum(d => d.Amount), hasOlder, from, end).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "GetHistoryAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<PayoutHistoryResponse>("Failed to load payout history");
        }
    }

    public async Task<IApiResponse<PayoutDayDetailDto>> GetDayAsync(DateOnly date, string? account)
    {
        try
        {
            if (!PayoutWindowCalculator.IsWorkingDay(date))
                return new PayoutDayDetailDto(date, 0m, []).ToOkApiResponse();

            var institution = await institutionRepo.GetOneAsync(i => i.Id == currentTenant.InstitutionId);
            var accounts = await AccountOptionsAsync(institution);
            account = NormaliseAccount(account, accounts);
            var accountNames = accounts.ToDictionary(a => a.Key, a => a.Name);

            var (start, end) = PayoutWindowCalculator.WindowFor(date);
            var items = new List<PayoutItemDto>();
            var mainName = accountNames.GetValueOrDefault(MainAccount) ?? "Main account";
            static string? NameOf(MemberSnapshot? m) => m is null ? null : $"{m.FirstName} {m.LastName}".Trim();

            var contributions = await contributionRepo.GetQueryable(c =>
                    c.Status == "Successful" && c.PaymentMethod == "Paystack" && c.ConfirmedAt != null && c.ConfirmedAt >= start && c.ConfirmedAt < end)
                .Select(c => new { c.CampaignId, c.Amount, c.ConfirmedAt, c.Member }).ToListAsync();
            var campaignIds = contributions.Select(c => c.CampaignId).Distinct().ToList();
            var accountOf = await ResolveAccountsAsync(campaignIds);
            var campaigns = campaignIds.Count == 0 ? [] : await campaignRepo.GetQueryable(c => campaignIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Title, c.IsMembershipCampaign }).ToDictionaryAsync(c => c.Id);
            foreach (var c in contributions)
            {
                var key = accountOf.GetValueOrDefault(c.CampaignId) ?? MainAccount;
                if (account is not null && key != account) continue;
                campaigns.TryGetValue(c.CampaignId, out var campaign);
                items.Add(new PayoutItemDto(campaign?.IsMembershipCampaign == true ? PayoutSources.Dues : PayoutSources.Fundraisers,
                    accountNames.GetValueOrDefault(key) ?? "Year group account", campaign?.Title ?? "Payment", NameOf(c.Member), c.Amount, c.ConfirmedAt!.Value));
            }

            var orders = await storeOrderRepo.GetQueryable(o =>
                    o.Status == "Successful" && o.PaymentMethod == "Paystack" && o.ConfirmedAt != null && o.ConfirmedAt >= start && o.ConfirmedAt < end)
                .Select(o => new { o.OrderNumber, o.TotalAmount, o.ConfirmedAt, o.Member }).ToListAsync();
            items.AddRange(orders.Select(o => new PayoutItemDto(PayoutSources.Store, mainName, $"Order {o.OrderNumber}", NameOf(o.Member), o.TotalAmount, o.ConfirmedAt!.Value)));

            var requests = await serviceRequestRepo.GetQueryable(r =>
                    r.PaymentStatus == "Successful" && r.PaymentMethod == "Paystack" && r.ConfirmedAt != null && r.ConfirmedAt >= start && r.ConfirmedAt < end)
                .Select(r => new { r.ServiceTypeName, r.Amount, r.ConfirmedAt, r.Member }).ToListAsync();
            items.AddRange(requests.Select(r => new PayoutItemDto(PayoutSources.Services, mainName, r.ServiceTypeName, NameOf(r.Member), r.Amount, r.ConfirmedAt!.Value)));

            var tickets = await eventRsvpRepo.GetQueryable(EventTicketRules.SettledBetween(start, end))
                .Select(t => new { t.EventId, t.MemberId, t.AmountPaid, t.PaidAt }).ToListAsync();
            var eventIds = tickets.Select(t => t.EventId).Distinct().ToList();
            // A ticket does not carry the payer's name (a member's snapshot is not stored on it), so it is looked up.
            var memberIds = tickets.Select(t => t.MemberId).Distinct().ToList();
            var people = memberIds.Count == 0 ? [] : await memberRepo.GetQueryable(m => memberIds.Contains(m.Id))
                .Select(m => new { m.Id, m.FirstName, m.LastName }).ToDictionaryAsync(m => m.Id, m => $"{m.FirstName} {m.LastName}".Trim());
            var titles = eventIds.Count == 0 ? [] : await eventRepo.GetQueryable(e => eventIds.Contains(e.Id)).Select(e => new { e.Id, e.Title }).ToDictionaryAsync(e => e.Id, e => e.Title);
            items.AddRange(tickets.Select(t => new PayoutItemDto(PayoutSources.EventRsvps, mainName, titles.GetValueOrDefault(t.EventId) ?? "RSVP", people.GetValueOrDefault(t.MemberId), t.AmountPaid ?? 0m, t.PaidAt!.Value)));

            // Store orders, services and event tickets always settle to the main account.
            if (account is not null) items = items.Where(i => i.Account == accountNames.GetValueOrDefault(account)).ToList();
            var ordered = items.OrderByDescending(i => i.ConfirmedAt).Take(500).ToList();
            return new PayoutDayDetailDto(date, items.Sum(i => i.Amount), ordered).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "GetDayAsync failed for {Date}", date);
            return ApiResponseExtensions.ToServerErrorApiResponse<PayoutDayDetailDto>("Failed to load that payout");
        }
    }

    public const string MainAccount = "main";

    /// <summary>
    /// The accounts this institution is paid into: its own, plus every year group whose dedicated account has been approved.
    /// Year groups that use the institution's account are not listed, since their money lands in the main one.
    /// </summary>
    private async Task<List<PayoutAccountDto>> AccountOptionsAsync(InstitutionEntity? institution)
    {
        var accounts = new List<PayoutAccountDto> { new(MainAccount, "Main account", BankLine(institution?.SettlementBankName, institution?.SettlementAccountNumber)) };
        var batches = await batchRepo.GetQueryable(b => b.PayoutStatus == "Approved" && !b.UseInstitutionAccount && b.PaystackSubaccountCode != null)
            .OrderByDescending(b => b.Year)
            .Select(b => new { b.Id, b.Name, b.SettlementBankName, b.SettlementAccountNumber }).ToListAsync();
        accounts.AddRange(batches.Select(b => new PayoutAccountDto(b.Id, b.Name, BankLine(b.SettlementBankName, b.SettlementAccountNumber))));
        return accounts;
    }

    private static string? BankLine(string? bank, string? number)
    {
        if (string.IsNullOrWhiteSpace(bank) && string.IsNullOrWhiteSpace(number)) return null;
        var tail = string.IsNullOrWhiteSpace(number) ? null : "ending " + number[^Math.Min(4, number.Length)..];
        return string.Join(" ", new[] { bank, tail }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    /// <summary>An unknown or empty account means all of them; a known one narrows the page to it.</summary>
    private static string? NormaliseAccount(string? account, List<PayoutAccountDto> accounts) =>
        !string.IsNullOrWhiteSpace(account) && account != "all" && accounts.Any(a => a.Key == account) ? account : null;

    /// <summary>
    /// A campaign targeting exactly one year group settles straight into THAT group's own Paystack subaccount once its payout
    /// setup is approved (see ContributionService.ResolveSubaccountAsync, which this mirrors); everything else settles to the
    /// institution's own account. Store orders, service requests and event tickets never resolve a year-group account, so only
    /// contributions need this. Returns, per campaign, the id of the year group it settles into; a campaign with no entry (or null)
    /// settles to the main account.
    /// </summary>
    private async Task<Dictionary<string, string?>> ResolveAccountsAsync(List<string> campaignIds)
    {
        if (campaignIds.Count == 0) return [];

        var campaigns = await campaignRepo.GetQueryable(c => campaignIds.Contains(c.Id)).Select(c => new { c.Id, c.YearGroups }).ToListAsync();
        var singleYear = campaigns.Where(c => c.YearGroups is { Count: 1 }).ToDictionary(c => c.Id, c => c.YearGroups![0]);
        if (singleYear.Count == 0) return [];

        var years = singleYear.Values.Distinct().ToList();
        var approved = (await batchRepo.GetQueryable(b =>
                years.Contains(b.Year) && b.PayoutStatus == "Approved" && !b.UseInstitutionAccount && b.PaystackSubaccountCode != null)
            .Select(b => new { b.Year, b.Id }).ToListAsync()).GroupBy(b => b.Year).ToDictionary(g => g.Key, g => g.First().Id);

        return singleYear.Where(kv => approved.ContainsKey(kv.Value)).ToDictionary(kv => kv.Key, kv => (string?)approved[kv.Value]);
    }
}

/// <summary>The kinds of payment a payout is made of, as the institution sees them.</summary>
public static class PayoutSources
{
    public const string Dues = "Dues";
    public const string Fundraisers = "Fundraisers";
    public const string Store = "Store";
    public const string Services = "Services";
    public const string EventRsvps = "Event RSVPs";
}
