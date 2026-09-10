using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services.Interfaces;

public interface IContributionService
{
    Task<IApiResponse<PgPagedResult<ContributionDto>>> GetMyContributionsAsync(string memberId, ContributionFilter filter);
    Task<IApiResponse<object>> InitiatePaystackPaymentAsync(InitiatePaystackPaymentRequest request, AuthData? member);
    Task<IApiResponse<object>> InitiateMembershipRenewalAsync(InitiateMembershipRenewalRequest request, AuthData member);
    Task<IApiResponse<object>> VerifyPaystackPaymentAsync(string reference, AuthData? member);
    Task<IApiResponse<object>> ProcessPaystackCallbackAsync(string reference, string rawBody);
    Task<IApiResponse<ContributionStatusResponse>> GetContributionStatusAsync(string reference, AuthData? member);
    Task<IApiResponse<ActivationStatusResponse>> GetActivationStatusAsync(string reference);
    Task<IApiResponse<MembershipStatusResponse>> GetMembershipStatusAsync(AuthData member);
    Task<IApiResponse<List<CampaignDto>>> GetCurrentYearUnpaidMembershipCampaignsAsync(AuthData member);
    Task<IApiResponse<ContributionDto>> UploadProofAsync(UploadContributionProofRequest request, AuthData member);

    /// <summary>The current member's standing monthly gifts, active and past.</summary>
    Task<IApiResponse<List<RecurringContributionDto>>> GetMyRecurringGivingAsync(string memberId);

    /// <summary>Cancels a standing monthly gift — terminal, the member must set up a new one (via a fresh InitiatePaystackPaymentAsync with SetupRecurringGiving) to resume.</summary>
    Task<IApiResponse<object>> CancelRecurringGivingAsync(string recurringContributionId, string memberId);
}
