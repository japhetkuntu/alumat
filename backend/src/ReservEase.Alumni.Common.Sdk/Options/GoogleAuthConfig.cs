namespace ReservEase.Alumni.Common.Sdk.Options;

public class GoogleAuthConfig
{
    /// <summary>The Web application OAuth 2.0 Client ID from Google Cloud Console — the only value
    /// Google's token verification actually needs (it checks the token's "aud" claim against this).</summary>
    public string ClientId { get; set; } = string.Empty;
}
