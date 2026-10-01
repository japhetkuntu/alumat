namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

// Platform marketing is intentionally separate from institutions' payment campaigns.
public class MarketingCampaign : BaseEntity
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "Draft";
}
public class MarketingPost : BaseEntity
{
    public string CampaignId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Caption { get; set; } = "";
    public string Content { get; set; } = "";
    public string DestinationPath { get; set; } = "/#onboard";
    public bool UseLandingPage { get; set; } = true;
    public string Status { get; set; } = "Draft";
    public string CaptionsJson { get; set; } = "{}";
    public string AssetIdsJson { get; set; } = "[]";
}
public class MarketingAsset : BaseEntity
{
    public string CampaignId { get; set; } = "";
    public string Url { get; set; } = "";
    public string Name { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
}
public class MarketingShare : BaseEntity
{
    public string CampaignId { get; set; } = "";
    public string PostId { get; set; } = "";
    public string Channel { get; set; } = "";
    public string Caption { get; set; } = "";
    // Immutable public content: subsequent edits never rewrite a prepared/shared version.
    public string SnapshotJson { get; set; } = "{}";
    public DateTime? SharedAt { get; set; }
    public string? PublishedUrl { get; set; }
    public bool Disabled { get; set; }
    // Short public-facing alternative to the 32-char Id in the share URL — see
    // MarketingRules.GenerateShortCode. Nullable so shares created before this
    // field existed keep resolving by their full Id (PublicMarketingController
    // matches either).
    public string? ShortCode { get; set; }
}
public class MarketingVisit : BaseEntity
{
    public string ShareId { get; set; } = "";
    public string SessionId { get; set; } = "";
}
