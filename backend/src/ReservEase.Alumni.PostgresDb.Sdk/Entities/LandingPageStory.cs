namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// One "why alumni join" card on the Member Portal's public landing page
/// (the Stories section). Stored as a jsonb list on <see cref="Institution"/>;
/// editable by both platform staff and the institution's own admins.
/// </summary>
public class LandingPageStory
{
    /// <summary>Lucide icon name (e.g. "Briefcase") — the frontend maps this to a component; unknown keys fall back to a default icon.</summary>
    public string Icon { get; set; } = "Briefcase";
    public string Eyebrow { get; set; } = string.Empty;
    public string Scenario { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}
