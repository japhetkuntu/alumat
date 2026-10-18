using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// A post the organiser adds to an event for the people who are going: a schedule change, the day's photos, the slides,
/// a recap. One update has a title, a category and any mix of <see cref="Blocks"/>, as many as it needs.
/// Only members with a confirmed seat read the content; everyone else sees that updates exist.
/// </summary>
public class EventUpdate : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>One of <see cref="EventUpdateRules.Categories"/>, or a short label the organiser typed.</summary>
    public string Category { get; set; } = string.Empty;
    /// <summary>Pinned updates stay at the top, newest first among themselves.</summary>
    public bool IsPinned { get; set; }
    public List<EventUpdateBlock> Blocks { get; set; } = [];
    public string? PostedByStaffId { get; set; }
    public string? PostedByName { get; set; }
}

public static class EventUpdateBlockTypes
{
    public const string Text = "Text";
    public const string Images = "Images";
    public const string Video = "Video";
    public const string Link = "Link";
    public const string Files = "Files";
    public static readonly string[] All = [Text, Images, Video, Link, Files];
}

/// <summary>One piece of an update. Which fields are used depends on <see cref="Type"/>.</summary>
public class EventUpdateBlock
{
    /// <summary>Stable id the client assigns, used to match uploaded files to the block they belong to.</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>Text, Images, Video, Link or Files.</summary>
    public string Type { get; set; } = EventUpdateBlockTypes.Text;
    /// <summary>An optional heading above the block.</summary>
    public string? Heading { get; set; }
    /// <summary>Text: the words. Link: an optional line describing it. Images and Files: an optional caption.</summary>
    public string? Text { get; set; }
    /// <summary>Video: the YouTube address. Link: the address.</summary>
    public string? Url { get; set; }
    /// <summary>Link: what to call it.</summary>
    public string? Label { get; set; }
    /// <summary>Images and Files: what was uploaded, in order.</summary>
    public List<EventUpdateItem> Items { get; set; } = [];
}

public class EventUpdateItem
{
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? ContentType { get; set; }
}
