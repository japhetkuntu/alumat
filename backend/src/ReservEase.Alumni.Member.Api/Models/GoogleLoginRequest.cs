namespace ReservEase.Alumni.Member.Api.Models;

/// <summary>The ID token minted client-side by Google Identity Services — verified server-side
/// before any account lookup happens (see IGoogleTokenVerifier). Never trust this token's claims
/// without that verification; it arrives over the wire, so it's caller-controlled input.</summary>
public record GoogleLoginRequest(string IdToken);
