namespace ReservEase.Alumni.Platform.Api.Models;

/// <summary>
/// One payout account on the platform's record: an institution's own, a year group's or a group's, with the details it was set up
/// with. Account numbers are masked here; a staff member reveals one on purpose and that is logged.
/// </summary>
public record PayoutAccountItem(
    string Kind,                       // "Institution" | "YearGroup" | "Group"
    string Id,
    string Name,
    string InstitutionId,
    string InstitutionName,
    string State,                      // "Live" (its own account) | "SharesInstitution" | "Pending" | "Rejected"
    string? BankName,
    string? AccountName,
    string? AccountNumberMasked,
    string? SubaccountCode,
    DateTime? DecidedAt,
    string? DecidedBy,
    string? DecisionNote,
    bool ChangeAwaitingApproval,       // a new submission is waiting while the details shown stay as approved
    bool ShowingSubmitted);            // the details shown were submitted and are not yet approved

public record PayoutAccountCounts(int Live, int Pending, int SharesInstitution, int Rejected);

public record PayoutAccountsResponse(
    PayoutAccountCounts Counts,
    List<PayoutAccountItem> Results,
    int TotalCount, int Page, int PageSize, int TotalPages);

public record RevealedAccountNumber(string AccountNumber);
