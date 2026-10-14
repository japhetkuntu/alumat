using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Extensions;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

public interface IIncentiveService
{
    Task<IApiResponse<IncentivesViewDto>> GetAsync();
    Task<IApiResponse<IncentiveEntryDto>> AddAsync(AddIncentiveEntryRequest request, string actorId, string actorName);
    Task<IApiResponse<IncentiveEntryDto>> VoidAsync(string entryId, string reason, string actorId, string actorName);
}

/// <summary>
/// The incentive ledger. A SuperAdmin records what a representative has earned and what they were paid (outside the system);
/// a representative sees their own lines read-only. Nothing here pays anyone.
/// </summary>
public class IncentiveService(
    IAlumniPgRepository<RepresentativeIncentiveEntry> entryRepo,
    IAlumniPgRepository<PlatformStaff> staffRepo,
    IAlumniPgRepository<Institution> institutionRepo,
    IAuditLogService auditLog,
    IStaffScope staffScope,
    IHttpContextAccessor http,
    ILogger<IncentiveService> logger) : IIncentiveService
{
    public const string Currency = "GHS";

    public async Task<IApiResponse<IncentivesViewDto>> GetAsync()
    {
        try
        {
            var visibility = await staffScope.GetAsync();
            var me = http.HttpContext!.User.GetAccount().Id;
            // A representative sees only their own ledger, not their team's: what someone earns is between them and the platform.
            var entries = visibility.Unrestricted
                ? (await entryRepo.GetAllAsync(_ => true)).ToList()
                : (await entryRepo.GetAllAsync(e => e.StaffId == me)).ToList();

            var staffIds = entries.Select(e => e.StaffId).Distinct().ToList();
            var reps = (await staffRepo.GetAllAsync(s => s.Role == PlatformStaffRoles.Representative && !s.IsDisabled || staffIds.Contains(s.Id))).ToList();
            if (!visibility.Unrestricted) reps = reps.Where(r => r.Id == me).ToList();
            var names = reps.ToDictionary(r => r.Id, r => r.Name);

            var instIds = entries.Where(e => e.InstitutionId != null).Select(e => e.InstitutionId!).Distinct().ToList();
            var institutions = instIds.Count == 0 ? new Dictionary<string, string>()
                : (await institutionRepo.GetAllAsync(i => instIds.Contains(i.Id), ignoreQueryFilters: true)).ToDictionary(i => i.Id, i => i.Name);

            var balances = reps.Select(r =>
            {
                var live = entries.Where(e => e.StaffId == r.Id && e.VoidedAt == null).ToList();
                var earned = live.Where(e => e.Kind == IncentiveKinds.Earned).Sum(e => e.Amount);
                var paid = live.Where(e => e.Kind == IncentiveKinds.Paid).Sum(e => e.Amount);
                var lastPaid = live.Where(e => e.Kind == IncentiveKinds.Paid).Select(e => (DateTime?)e.OccurredOn).Max();
                return new IncentiveBalanceDto(r.Id, r.Name, earned, paid, earned - paid, lastPaid);
            }).OrderByDescending(b => b.Owed).ThenBy(b => b.StaffName).ToList();

            var rows = entries.OrderByDescending(e => e.OccurredOn).ThenByDescending(e => e.CreatedAt).Take(500)
                .Select(e => ToDto(e, names.GetValueOrDefault(e.StaffId, "Former team member"),
                    e.InstitutionId != null ? institutions.GetValueOrDefault(e.InstitutionId) : null)).ToList();

            return new IncentivesViewDto(visibility.Unrestricted, Currency, balances, rows).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Incentives GetAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<IncentivesViewDto>("Failed to load incentives");
        }
    }

    public async Task<IApiResponse<IncentiveEntryDto>> AddAsync(AddIncentiveEntryRequest request, string actorId, string actorName)
    {
        try
        {
            var rep = await staffRepo.GetOneAsync(s => s.Id == request.StaffId);
            if (rep is null || rep.Role != PlatformStaffRoles.Representative)
                return ApiResponseExtensions.ToBadRequestApiResponse<IncentiveEntryDto>("Choose a representative.");
            if (!IncentiveKinds.IsValid(request.Kind))
                return ApiResponseExtensions.ToBadRequestApiResponse<IncentiveEntryDto>("Kind must be Earned or Paid.");

            string? institutionName = null;
            if (!string.IsNullOrWhiteSpace(request.InstitutionId))
            {
                var inst = await institutionRepo.GetOneAsync(i => i.Id == request.InstitutionId);
                if (inst is null) return ApiResponseExtensions.ToBadRequestApiResponse<IncentiveEntryDto>("That institution doesn't exist.");
                institutionName = inst.Name;
            }

            if (request.Kind == IncentiveKinds.Paid)
            {
                var live = (await entryRepo.GetAllAsync(e => e.StaffId == rep.Id && e.VoidedAt == null)).ToList();
                var owed = live.Where(e => e.Kind == IncentiveKinds.Earned).Sum(e => e.Amount) - live.Where(e => e.Kind == IncentiveKinds.Paid).Sum(e => e.Amount);
                if (request.Amount > owed)
                    return ApiResponseExtensions.ToBadRequestApiResponse<IncentiveEntryDto>(
                        $"{rep.Name} is owed {Currency} {owed:0.00}, so a payment of {Currency} {request.Amount:0.00} is more than they are owed. Record what they earned first.");
            }

            var entry = new RepresentativeIncentiveEntry
            {
                StaffId = rep.Id,
                Kind = request.Kind,
                Amount = Math.Round(request.Amount, 2),
                Reason = request.Reason.Trim(),
                Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
                InstitutionId = string.IsNullOrWhiteSpace(request.InstitutionId) ? null : request.InstitutionId,
                OccurredOn = (request.OccurredOn ?? DateTime.UtcNow).Date,
                RecordedByStaffId = actorId,
                RecordedByName = actorName,
                CreatedBy = actorId,
            };
            await entryRepo.AddAsync(entry);
            await auditLog.LogAsync(actorId, actorName, $"recorded incentive {entry.Kind.ToLowerInvariant()} {Currency} {entry.Amount:0.00}", rep.Name);
            return ToDto(entry, rep.Name, institutionName).ToCreatedApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Incentives AddAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<IncentiveEntryDto>("Failed to record the entry");
        }
    }

    public async Task<IApiResponse<IncentiveEntryDto>> VoidAsync(string entryId, string reason, string actorId, string actorName)
    {
        try
        {
            var entry = await entryRepo.GetOneAsync(e => e.Id == entryId);
            if (entry is null) return ApiResponseExtensions.ToNotFoundApiResponse<IncentiveEntryDto>("Entry not found");
            if (entry.VoidedAt != null) return ApiResponseExtensions.ToBadRequestApiResponse<IncentiveEntryDto>("This entry is already voided.");

            // Voiding an earning must not leave a payment larger than what remains owed.
            if (entry.Kind == IncentiveKinds.Earned)
            {
                var live = (await entryRepo.GetAllAsync(e => e.StaffId == entry.StaffId && e.VoidedAt == null && e.Id != entry.Id)).ToList();
                var remaining = live.Where(e => e.Kind == IncentiveKinds.Earned).Sum(e => e.Amount) - live.Where(e => e.Kind == IncentiveKinds.Paid).Sum(e => e.Amount);
                if (remaining < 0)
                    return ApiResponseExtensions.ToBadRequestApiResponse<IncentiveEntryDto>("Void the payments made against this earning first, or the balance would go negative.");
            }

            entry.VoidedAt = DateTime.UtcNow;
            entry.VoidReason = reason.Trim();
            entry.VoidedByName = actorName;
            entry.UpdatedBy = actorId;
            await entryRepo.UpdateAsync(entry);

            var rep = await staffRepo.GetOneAsync(s => s.Id == entry.StaffId);
            await auditLog.LogAsync(actorId, actorName, $"voided incentive {entry.Kind.ToLowerInvariant()} {Currency} {entry.Amount:0.00}", rep?.Name ?? entry.StaffId);
            return ToDto(entry, rep?.Name ?? "Former team member", null).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Incentives VoidAsync failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<IncentiveEntryDto>("Failed to void the entry");
        }
    }

    private static IncentiveEntryDto ToDto(RepresentativeIncentiveEntry e, string staffName, string? institutionName) => new(
        e.Id, e.StaffId, staffName, e.Kind, e.Amount, e.Reason, e.Reference, e.InstitutionId, institutionName,
        e.OccurredOn, e.RecordedByName, e.CreatedAt, e.VoidedAt != null, e.VoidReason, e.VoidedByName);
}
