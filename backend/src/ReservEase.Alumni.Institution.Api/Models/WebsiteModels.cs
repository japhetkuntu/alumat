using Newtonsoft.Json.Linq;

namespace ReservEase.Alumni.Institution.Api.Models;

/// <summary>Where the website stands. "HasUnpublishedChanges" is true when the draft has been saved since it was last published.</summary>
public record WebsiteStatusDto(bool IsLive, DateTime? PublishedAt, string? PublishedBy, int DraftRevision, int PublishedRevision, bool HasUnpublishedChanges);

/// <summary>The institution's own branding, so the editor can show it and the website can start with it. Read from the portal theme, never edited here.</summary>
public record WebsiteBrandDto(string Name, string? Tagline, string PrimaryColor, string? SecondaryColor, string? LogoUrl, string Slug);

/// <summary>Everything the editor needs in one call. Draft is null until the institution first opens the builder.</summary>
public record WebsiteEditorDto(WebsiteStatusDto Status, JRaw? Draft, WebsiteBrandDto Brand, List<string> DisabledFeatures);

public class SaveWebsiteDraftRequest
{
    /// <summary>The whole page, as the editor holds it. The server cleans it before storing.</summary>
    public JObject? Document { get; set; }
    public int BaseRevision { get; set; }
}

public class PublishWebsiteRequest
{
    public int Revision { get; set; }
}

public record WebsiteVersionDto(string Id, int Revision, DateTime PublishedAt, string PublishedBy);
