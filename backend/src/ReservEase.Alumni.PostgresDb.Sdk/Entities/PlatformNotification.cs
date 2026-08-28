namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// An in-app notification for platform staff — not tenant-scoped (platform
/// staff aren't tied to one institution). Currently only raised when an
/// institution opens a support ticket. One row per recipient staff member
/// (not a single shared broadcast row) so each person's read state is their
/// own — mirrors how the tenant-scoped <see cref="Notification"/> fans out
/// one row per institution admin.
/// </summary>
public class PlatformNotification : BaseEntity
{
    public string RecipientStaffId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    /// <summary>SupportTicketOpened</summary>
    public string Type { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public string? ActionUrl { get; set; }
}
