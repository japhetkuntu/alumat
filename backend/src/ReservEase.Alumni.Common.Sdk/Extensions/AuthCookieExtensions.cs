using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;

namespace ReservEase.Alumni.Common.Sdk.Extensions;

/// <summary>
/// Shared across all 3 APIs (Member/Institution/Platform) — httpOnly cookies are
/// the actual credential transport now, not just an Authorization-header
/// convenience, so login/google-login/refresh/logout across every API set and
/// clear the exact same two cookies the same way.
/// </summary>
public static class AuthCookieExtensions
{
    public const string AccessTokenCookieName = "access_token";
    public const string RefreshTokenCookieName = "refresh_token";

    public static void SetAuthCookies(
        this HttpResponse response, string accessToken, string refreshToken, TimeSpan accessLifetime, TimeSpan refreshLifetime)
    {
        // The access-token cookie's browser-side MaxAge deliberately outlives the JWT's
        // own `exp` claim (accessLifetime) — actual auth is still gated by JWT validation
        // rejecting the expired token, but /auth/refreshtoken needs to read this cookie
        // to identify *who* is refreshing (see ExtractUserIdFromExpiredToken) even after
        // the JWT itself has expired. If the cookie expired at the same moment as the
        // JWT, the browser would stop sending it right when refresh needs it most,
        // forcing a full re-login every accessLifetime instead of a silent refresh.
        response.Cookies.Append(AccessTokenCookieName, accessToken, CookieOptionsFor(refreshLifetime));
        response.Cookies.Append(RefreshTokenCookieName, refreshToken, CookieOptionsFor(refreshLifetime));
    }

    public static void ClearAuthCookies(this HttpResponse response)
    {
        response.Cookies.Delete(AccessTokenCookieName, CookieOptionsFor(TimeSpan.Zero));
        response.Cookies.Delete(RefreshTokenCookieName, CookieOptionsFor(TimeSpan.Zero));
    }

    // Secure requires HTTPS — true in every real deployment (nginx terminates
    // TLS) and only bites in bare-HTTP local dev, where the browser simply
    // won't set the cookie and Swagger's Authorization-header path still works.
    private static CookieOptions CookieOptionsFor(TimeSpan maxAge) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = maxAge,
    };

    /// <summary>
    /// Lets the access token travel as the httpOnly cookie instead of an
    /// Authorization header, for browser requests that no longer hold the
    /// token in JS-readable storage. Header still wins when both are present
    /// (Swagger, service-to-service calls).
    /// </summary>
    public static void UseAccessTokenCookieFallback(this JwtBearerOptions options)
    {
        options.Events ??= new JwtBearerEvents();
        options.Events.OnMessageReceived = context =>
        {
            if (string.IsNullOrEmpty(context.Token) && context.Request.Cookies.TryGetValue(AccessTokenCookieName, out var cookieToken))
            {
                context.Token = cookieToken;
            }
            return Task.CompletedTask;
        };
    }
}
