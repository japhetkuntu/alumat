using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Integrations;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public interface IIntegrationStatusService
{
    Task<IApiResponse<IntegrationMemberStatusResponse>> GetMemberStatusAsync(string? apiKey, string? memberId);
}

/// <summary>
/// The status check partner portals call from their servers. The API key says which institution is asking, so nothing here depends on
/// the host the request came in on. A student ID that exists is answered from the cache when it can be; one that does not is given a
/// fresh registration link.
/// </summary>
public class IntegrationStatusService(
    IntegrationService integrations,
    IntegrationStatusCache cache,
    ICurrentTenantService currentTenant,
    IConfiguration configuration,
    ILogger<IntegrationStatusService> logger) : IIntegrationStatusService
{
    public async Task<IApiResponse<IntegrationMemberStatusResponse>> GetMemberStatusAsync(string? apiKey, string? memberId)
    {
        try
        {
            var caller = await integrations.AuthenticateAsync(apiKey);
            if (caller is null)
                return ApiResponseExtensions.ToUnauthorizedApiResponse<IntegrationMemberStatusResponse>("The API key is missing, wrong or has been revoked.");
            if (caller.InstitutionSuspended || !caller.Enabled)
                return ApiResponseExtensions.ToForbiddenApiResponse<IntegrationMemberStatusResponse>("This integration is not enabled for the institution.");

            var studentId = IntegrationSecrets.NormaliseStudentId(memberId);
            if (studentId is null)
                return ApiResponseExtensions.ToBadRequestApiResponse<IntegrationMemberStatusResponse>("memberId is required and must be at most 64 characters.");

            // The key, not the host, decides the institution for everything the rest of this request reads or writes.
            currentTenant.SetInstitutionId(caller.InstitutionId, caller.InstitutionSlug);

            if (await cache.KnownToExistAsync(caller.InstitutionId, studentId))
                return new IntegrationMemberStatusResponse(true, null, null).ToOkApiResponse("Member exists.");

            if (await integrations.StudentExistsAsync(caller.InstitutionId, studentId))
            {
                await cache.RememberAsync(caller.InstitutionId, studentId, caller.StatusCacheDays);
                return new IntegrationMemberStatusResponse(true, null, null).ToOkApiResponse("Member exists.");
            }

            var portal = MemberPortalLinks.Url(caller.InstitutionSlug, caller.CustomDomain, configuration["MemberPortalBaseDomain"]);
            if (portal is null)
            {
                logger.LogError("Integration status check for institution {InstitutionId}: no member portal address is configured", caller.InstitutionId);
                return ApiResponseExtensions.ToServerErrorApiResponse<IntegrationMemberStatusResponse>("The institution's member portal address is not set up.");
            }

            var (token, expiresAt) = await integrations.IssueLinkAsync(caller.InstitutionId, studentId, caller.LinkExpiryHours);
            return new IntegrationMemberStatusResponse(false, $"{portal}/register?ob={Uri.EscapeDataString(token)}", expiresAt)
                .ToOkApiResponse("Member does not exist. Send the student to registrationUrl to register.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Integration status check failed");
            return ApiResponseExtensions.ToServerErrorApiResponse<IntegrationMemberStatusResponse>("The status check failed. Try again.");
        }
    }
}
