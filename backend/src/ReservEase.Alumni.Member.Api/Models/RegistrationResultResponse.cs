namespace ReservEase.Alumni.Member.Api.Models;

/// <summary>
/// Tells the frontend whether this registration landed the member as
/// immediately "Active" (institution.AutoApproveMembers is on) or "Pending"
/// admin review — the register page shows a different success screen for
/// each rather than always claiming "your account is pending approval",
/// which would be wrong when a member is actually already active.
/// </summary>
/// <param name="PortalName">Set when the member came through a partner link: where they can return to, so the success screen can offer it.</param>
/// <param name="Session">Set when the member is approved on the spot: they have just proved their email (code or Google), so they are signed in
/// instead of being sent to the sign-in page. Null for a member waiting on approval, who cannot sign in yet.</param>
public record RegistrationResultResponse(bool Approved, string? PortalName = null, string? RedirectUrl = null, MemberTokenResponse? Session = null);

/// <summary>State is valid, expired, used, replaced or notfound. StudentId is only given for a valid link.</summary>
public record OnboardingLinkInfo(string State, string? StudentId, string? PortalName, string? RedirectUrl);
