using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.Common.Sdk.Options;

namespace ReservEase.Alumni.Common.Sdk.Services;

public class GoogleTokenVerifier(IOptions<GoogleAuthConfig> configOptions, ILogger<GoogleTokenVerifier> logger)
    : IGoogleTokenVerifier
{
    private readonly GoogleAuthConfig config = configOptions.Value;

    public async Task<GoogleIdentity?> VerifyAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(config.ClientId))
        {
            logger.LogError("Google sign-in attempted but GoogleAuthConfig:ClientId is not configured");
            return null;
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [config.ClientId],
            });

            // Google only issues a verified token for a verified email in the first place, but this
            // is cheap insurance against a payload that's technically valid but for an unverified
            // address — this app matches accounts by email, so that address must be trustworthy.
            if (!payload.EmailVerified)
            {
                logger.LogWarning("Google sign-in rejected — email {Email} is not verified by Google", payload.Email);
                return null;
            }

            return new GoogleIdentity(
                payload.Email.ToLower().Trim(),
                payload.GivenName ?? payload.Name ?? "Member",
                payload.FamilyName ?? "",
                payload.Picture);
        }
        catch (InvalidJwtException e)
        {
            logger.LogWarning(e, "Google ID token failed verification");
            return null;
        }
    }
}
