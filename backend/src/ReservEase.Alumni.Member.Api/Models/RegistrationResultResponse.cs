namespace ReservEase.Alumni.Member.Api.Models;

/// <summary>
/// Tells the frontend whether this registration landed the member as
/// immediately "Active" (institution.AutoApproveMembers is on) or "Pending"
/// admin review — the register page shows a different success screen for
/// each rather than always claiming "your account is pending approval",
/// which would be wrong when a member is actually already active.
/// </summary>
public record RegistrationResultResponse(bool Approved);
