namespace ReservEase.Alumni.Common.Sdk.Services;

public record GoogleIdentity(string Email, string FirstName, string LastName, string? PictureUrl);

public interface IGoogleTokenVerifier
{
    /// <summary>
    /// Verifies a Google Identity Services ID token against this API's configured OAuth client ID
    /// (signature, issuer, expiry, and audience are all checked by the underlying Google library —
    /// this is NOT a bearer token this app issued, so it must never be trusted without that check).
    /// Returns null for anything that fails verification — an invalid/expired/forged token, or one
    /// minted for a different app's client ID.
    /// </summary>
    Task<GoogleIdentity?> VerifyAsync(string idToken);
}
