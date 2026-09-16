namespace ReservEase.Alumni.Notifications.Sdk.Models;

/// <summary>An admin-composed broadcast's already-resolved recipient — the workflow
/// can't re-derive this list itself (it comes from whatever ad hoc filter the admin
/// applied in the composer UI), so it travels in the request as-is.</summary>
public record BroadcastRecipient(string Id, string Email, string FirstName, string? Phone);
