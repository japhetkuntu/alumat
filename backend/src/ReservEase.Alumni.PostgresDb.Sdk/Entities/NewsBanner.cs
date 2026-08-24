namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// The dismissible announcement strip at the top of the Member Portal's public
/// landing page. Stored as a jsonb object on <see cref="Institution"/>, null by
/// default (no banner shown); editable by both platform staff and the
/// institution's own admins.
/// </summary>
public class NewsBanner
{
    public bool Enabled { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? LinkText { get; set; }
    /// <summary>Either an absolute URL or an in-page anchor like "#spotlight".</summary>
    public string? LinkUrl { get; set; }
}
