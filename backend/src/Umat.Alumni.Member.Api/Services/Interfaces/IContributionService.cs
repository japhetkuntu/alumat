using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Member.Api.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Models;

namespace Umat.Alumni.Member.Api.Services.Interfaces;

public interface IContributionService
{
    Task<IApiResponse<PgPagedResult<ContributionDto>>> GetMyContributionsAsync(string memberId, ContributionFilter filter);
    Task<IApiResponse<object>> InitiatePaystackPaymentAsync(InitiatePaystackPaymentRequest request, AuthData? member);
    Task<IApiResponse<object>> InitiateMembershipRenewalAsync(InitiateMembershipRenewalRequest request, AuthData member);
    Task<IApiResponse<object>> VerifyPaystackPaymentAsync(string reference, AuthData? member);
    Task<IApiResponse<object>> ProcessPaystackCallbackAsync(string reference, string rawBody);
    Task<IApiResponse<ContributionStatusResponse>> GetContributionStatusAsync(string reference, AuthData member);
    Task<IApiResponse<ActivationStatusResponse>> GetActivationStatusAsync(string reference);
    Task<IApiResponse<MembershipStatusResponse>> GetMembershipStatusAsync(AuthData member);
    Task<IApiResponse<List<CampaignDto>>> GetCurrentYearUnpaidMembershipCampaignsAsync(AuthData member);
    Task<IApiResponse<ContributionDto>> UploadProofAsync(UploadContributionProofRequest request, AuthData member);
}
