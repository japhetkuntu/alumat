namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>A broadcast message platform staff sent to institution admins. Global, not tenant-scoped.</summary>
public class Announcement : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Audience { get; set; } = "All institutions";
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    /// <summary>Snapshot of how many institution admins were in the audience at send time.</summary>
    public int TotalAdmins { get; set; }
    /// <summary>No read-receipt tracking exists yet; always 0 until that's built.</summary>
    public int SeenByAdmins { get; set; }
}
