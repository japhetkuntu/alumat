using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using BatchEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Batch;
using CommunityEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Community;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

/// <summary>
/// The platform's record of every payout account a community has given us. Approval used to be the end of the story: the details went
/// live and nothing showed them again. This keeps them viewable, by institution, kind and state, along with who decided and when.
/// Every query ignores the tenant filter, as this service is not scoped to one institution.
/// </summary>
public class PayoutAccountService(
    IAlumniPgRepository<Institution> institutionRepo, IAlumniPgRepository<BatchEntity> batchRepo, IAlumniPgRepository<CommunityEntity> groupRepo,
    IAuditLogService auditLog, ILogger<PayoutAccountService> logger) : IPayoutAccountService
{
    public const string KindInstitution = "Institution";
    public const string KindYearGroup = "YearGroup";
    public const string KindGroup = "Group";
    private const int MaxPageSize = 100;

    public async Task<IApiResponse<PayoutAccountsResponse>> GetAsync(string? institutionId, string? kind, string? state, string? search, int page, int pageSize)
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var all = await LoadAsync(institutionId);

            var term = search?.Trim();
            if (!string.IsNullOrEmpty(term))
                all = all.Where(a => Matches(a, term)).ToList();

            var counts = new PayoutAccountCounts(
                all.Count(a => a.State == "Live"), all.Count(a => a.State == "Pending"),
                all.Count(a => a.State == "SharesInstitution"), all.Count(a => a.State == "Rejected"));

            IEnumerable<PayoutAccountItem> shown = all;
            if (!string.IsNullOrEmpty(kind) && kind != "all") shown = shown.Where(a => a.Kind == kind);
            if (!string.IsNullOrEmpty(state) && state != "all") shown = shown.Where(a => a.State == state);

            var ordered = shown.OrderBy(a => a.InstitutionName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => KindOrder(a.Kind)).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var totalPages = Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)pageSize));
            page = Math.Min(page, totalPages);

            return new PayoutAccountsResponse(counts, ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList(), ordered.Count, page, pageSize, totalPages).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error listing payout accounts");
            return ApiResponseExtensions.ToServerErrorApiResponse<PayoutAccountsResponse>("Failed to load the payout accounts");
        }
    }

    public async Task<IApiResponse<RevealedAccountNumber>> RevealAsync(string kind, string id, string actorId, string actorName)
    {
        try
        {
            string? number; string name; string institutionName;
            switch (kind)
            {
                case KindInstitution:
                {
                    var i = await institutionRepo.GetOneAsync(x => x.Id == id, ignoreQueryFilters: true);
                    if (i is null) return ApiResponseExtensions.ToNotFoundApiResponse<RevealedAccountNumber>("Account not found");
                    number = i.SettlementAccountNumber ?? i.PendingPayoutChanges?.SettlementAccountNumber; name = i.Name; institutionName = i.Name;
                    break;
                }
                case KindYearGroup:
                {
                    var b = await batchRepo.GetOneAsync(x => x.Id == id, ignoreQueryFilters: true);
                    if (b is null) return ApiResponseExtensions.ToNotFoundApiResponse<RevealedAccountNumber>("Account not found");
                    number = b.SettlementAccountNumber ?? b.PendingPayoutChanges?.SettlementAccountNumber; name = b.Name;
                    institutionName = (await institutionRepo.GetOneAsync(x => x.Id == b.InstitutionId, ignoreQueryFilters: true))?.Name ?? b.InstitutionId;
                    break;
                }
                case KindGroup:
                {
                    var g = await groupRepo.GetOneAsync(x => x.Id == id, ignoreQueryFilters: true);
                    if (g is null) return ApiResponseExtensions.ToNotFoundApiResponse<RevealedAccountNumber>("Account not found");
                    number = g.SettlementAccountNumber ?? g.PendingPayoutChanges?.SettlementAccountNumber; name = g.Name;
                    institutionName = (await institutionRepo.GetOneAsync(x => x.Id == g.InstitutionId, ignoreQueryFilters: true))?.Name ?? g.InstitutionId;
                    break;
                }
                default:
                    return ApiResponseExtensions.ToBadRequestApiResponse<RevealedAccountNumber>("Unknown account type");
            }

            if (string.IsNullOrWhiteSpace(number))
                return ApiResponseExtensions.ToNotFoundApiResponse<RevealedAccountNumber>("This account has no account number on record");

            // Seeing a full account number is a deliberate act, so it leaves a trail: who looked, at whose account.
            await auditLog.LogAsync(actorId, actorName, $"viewed the account number of {(kind == KindYearGroup ? "year group" : kind.ToLowerInvariant())} \"{name}\"", institutionName);
            return new RevealedAccountNumber(number).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error revealing payout account number {Kind}/{Id}", kind, id);
            return ApiResponseExtensions.ToServerErrorApiResponse<RevealedAccountNumber>("Failed to reveal the account number");
        }
    }

    private async Task<List<PayoutAccountItem>> LoadAsync(string? institutionId)
    {
        var institutions = (await institutionRepo.GetAllAsync(i => institutionId == null || i.Id == institutionId, ignoreQueryFilters: true)).ToList();
        var names = institutions.ToDictionary(i => i.Id, i => i.Name);
        var items = new List<PayoutAccountItem>();

        // An institution that has never submitted its own account has nothing to show; only ones with a payout status beyond "None" are listed.
        foreach (var i in institutions.Where(i => i.PayoutStatus != "None"))
            items.Add(Build(KindInstitution, i.Id, i.Name, i.Id, i.Name, i.PayoutStatus, false, i.SettlementBankName, i.SettlementAccountName, i.SettlementAccountNumber,
                i.PaystackSubaccountCode, i.PayoutDecidedAt, i.PayoutDecidedBy, i.PayoutDecisionNote,
                i.PendingPayoutChanges is null ? null : (i.PendingPayoutChanges.SettlementBankName, i.PendingPayoutChanges.SettlementAccountName, i.PendingPayoutChanges.SettlementAccountNumber)));

        var batches = (await batchRepo.GetAllAsync(b => b.PayoutStatus != "None" && (institutionId == null || b.InstitutionId == institutionId), ignoreQueryFilters: true)).ToList();
        foreach (var b in batches)
            items.Add(Build(KindYearGroup, b.Id, b.Name, b.InstitutionId, names.GetValueOrDefault(b.InstitutionId) ?? "Unknown institution", b.PayoutStatus, b.UseInstitutionAccount,
                b.SettlementBankName, b.SettlementAccountName, b.SettlementAccountNumber, b.PaystackSubaccountCode, b.PayoutDecidedAt, b.PayoutDecidedBy, b.PayoutDecisionNote,
                b.PendingPayoutChanges is null ? null : (b.PendingPayoutChanges.SettlementBankName, b.PendingPayoutChanges.SettlementAccountName, b.PendingPayoutChanges.SettlementAccountNumber)));

        var groups = (await groupRepo.GetAllAsync(g => g.PayoutStatus != "None" && (institutionId == null || g.InstitutionId == institutionId), ignoreQueryFilters: true)).ToList();
        foreach (var g in groups)
            items.Add(Build(KindGroup, g.Id, g.Name, g.InstitutionId, names.GetValueOrDefault(g.InstitutionId) ?? "Unknown institution", g.PayoutStatus, g.UseInstitutionAccount,
                g.SettlementBankName, g.SettlementAccountName, g.SettlementAccountNumber, g.PaystackSubaccountCode, g.PayoutDecidedAt, g.PayoutDecidedBy, g.PayoutDecisionNote,
                g.PendingPayoutChanges is null ? null : (g.PendingPayoutChanges.SettlementBankName, g.PendingPayoutChanges.SettlementAccountName, g.PendingPayoutChanges.SettlementAccountNumber)));

        return items;
    }

    private static PayoutAccountItem Build(string kind, string id, string name, string institutionId, string institutionName, string payoutStatus, bool usesInstitutionAccount,
        string? bank, string? accountName, string? number, string? subaccount, DateTime? decidedAt, string? decidedBy, string? note,
        (string? Bank, string? AccountName, string? Number)? pending)
    {
        var state = payoutStatus switch
        {
            "Approved" when usesInstitutionAccount => "SharesInstitution",
            "Approved" => "Live",
            "Pending" => "Pending",
            "Rejected" => "Rejected",
            _ => payoutStatus,
        };
        // The details on record are the approved ones. A first submission has none yet, so what was submitted is shown, and said to be.
        var hasApproved = !string.IsNullOrWhiteSpace(number) || !string.IsNullOrWhiteSpace(bank);
        var showSubmitted = !hasApproved && pending is not null;
        if (showSubmitted) (bank, accountName, number) = (pending!.Value.Bank, pending.Value.AccountName, pending.Value.Number);

        return new PayoutAccountItem(kind, id, name, institutionId, institutionName, state, bank, accountName, Mask(number), subaccount,
            decidedAt, decidedBy, note, hasApproved && pending is not null, showSubmitted);
    }

    private static bool Matches(PayoutAccountItem a, string term) =>
        new[] { a.Name, a.InstitutionName, a.BankName, a.AccountName, a.SubaccountCode }
            .Any(v => v is not null && v.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static int KindOrder(string kind) => kind switch { KindInstitution => 0, KindYearGroup => 1, _ => 2 };

    /// <summary>Only the last four digits, so a glance at the page never gives away a whole account number.</summary>
    private static string? Mask(string? number)
    {
        if (string.IsNullOrWhiteSpace(number)) return null;
        var digits = number.Trim();
        return digits.Length <= 4 ? digits : "•••• " + digits[^4..];
    }
}
