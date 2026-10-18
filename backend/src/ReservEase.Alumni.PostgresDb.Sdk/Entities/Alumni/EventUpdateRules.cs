namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// What an event update may contain. The limits are generous on purpose: an organiser should be able to post a whole day's
/// photos, or a handful of documents, without hitting a wall. They exist to stop a mistake or abuse, not to shape the content.
/// </summary>
public static class EventUpdateRules
{
    /// <summary>The usual kinds of update. An organiser may type their own label instead.</summary>
    public static readonly string[] Categories = ["Announcement", "Schedule", "Reminder", "Photos & video", "Materials", "Recap"];

    public const int MaxTitleLength = 150;
    public const int MaxCategoryLength = 40;
    public const int MaxHeadingLength = 120;
    public const int MaxTextLength = 10_000;
    public const int MaxBlocks = 50;
    public const int MaxItemsPerBlock = 60;
    public const long MaxImageBytes = 15L * 1024 * 1024;
    public const long MaxFileBytes = 30L * 1024 * 1024;
    /// <summary>Everything uploaded in one save.</summary>
    public const long MaxUploadBytes = 300L * 1024 * 1024;

    public static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    public static readonly string[] FileExtensions = [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".rtf", ".zip"];

    public static bool IsImage(string fileName) => ImageExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());
    public static bool IsDocument(string fileName) => FileExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());

    /// <summary>Only web addresses: anything else (javascript:, data:, file:) is refused so a link can never run code.</summary>
    public static bool IsWebUrl(string? value, out Uri? uri)
    {
        uri = null;
        return Uri.TryCreate(value?.Trim(), UriKind.Absolute, out uri) && uri.Scheme is "http" or "https";
    }

    /// <summary>The 11-character video id from a YouTube address, or null when it is not one.</summary>
    public static string? YouTubeId(string? value)
    {
        if (!IsWebUrl(value, out var uri) || uri is null) return null;
        var host = uri.Host.ToLowerInvariant();
        string? id = null;
        if (host is "youtu.be") id = uri.AbsolutePath.Trim('/').Split('/')[0];
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "youtube-nocookie.com" or "www.youtube-nocookie.com")
        {
            if (uri.AbsolutePath.StartsWith("/watch", StringComparison.OrdinalIgnoreCase))
                id = System.Web.HttpUtility.ParseQueryString(uri.Query)["v"];
            else
            {
                var parts = uri.AbsolutePath.Trim('/').Split('/');
                if (parts.Length >= 2 && parts[0] is "embed" or "shorts" or "live" or "v") id = parts[1];
            }
        }
        return id is { Length: 11 } && id.All(c => char.IsLetterOrDigit(c) || c is '_' or '-') ? id : null;
    }

    public static string YouTubeUrl(string id) => $"https://www.youtube.com/watch?v={id}";

    /// <summary>
    /// Checks the shape of an update's blocks and tidies them (trims text, turns a video address into its standard form).
    /// <paramref name="keepableUrls"/> are the file addresses already saved on this update, which the organiser may keep.
    /// Returns an error to show, or null when the blocks are fine.
    /// </summary>
    public static string? ValidateAndClean(List<EventUpdateBlock> blocks, IReadOnlySet<string> keepableUrls, IReadOnlyDictionary<string, int> newFileCounts)
    {
        if (blocks.Count == 0) return "Add something to the update: some text, photos, a video, a link or files.";
        if (blocks.Count > MaxBlocks) return $"An update can have up to {MaxBlocks} parts. Split this into two updates.";
        var ids = new HashSet<string>();
        foreach (var b in blocks)
        {
            if (string.IsNullOrWhiteSpace(b.Id) || !ids.Add(b.Id)) return "Something went wrong with the update's layout. Please try again.";
            if (!EventUpdateBlockTypes.All.Contains(b.Type)) return "One of the parts has an unknown type.";
            b.Heading = Trim(b.Heading);
            if (b.Heading is { Length: > MaxHeadingLength }) return $"A heading can be up to {MaxHeadingLength} characters.";
            b.Text = Trim(b.Text);
            if (b.Text is { Length: > MaxTextLength }) return $"A text part can be up to {MaxTextLength:N0} characters.";
            b.Label = Trim(b.Label);
            if (b.Label is { Length: > MaxHeadingLength }) return $"A link name can be up to {MaxHeadingLength} characters.";
            b.Url = Trim(b.Url);

            var newCount = newFileCounts.GetValueOrDefault(b.Id);
            switch (b.Type)
            {
                case EventUpdateBlockTypes.Text:
                    if (string.IsNullOrEmpty(b.Text)) return "A text part can't be empty.";
                    b.Items = [];
                    b.Url = null;
                    break;
                case EventUpdateBlockTypes.Video:
                    var id = YouTubeId(b.Url);
                    if (id is null) return "A video needs a YouTube address.";
                    b.Url = YouTubeUrl(id);
                    b.Items = [];
                    break;
                case EventUpdateBlockTypes.Link:
                    if (!IsWebUrl(b.Url, out var uri)) return "A link needs a web address starting with http:// or https://.";
                    b.Url = uri!.ToString();
                    b.Items = [];
                    break;
                default: // Images, Files
                    if (b.Url is not null) b.Url = null;
                    foreach (var item in b.Items)
                        if (!keepableUrls.Contains(item.Url)) return "A file in this update could not be kept. Please add it again.";
                    if (b.Items.Count + newCount == 0) return b.Type == EventUpdateBlockTypes.Images ? "A photos part needs at least one photo." : "A files part needs at least one file.";
                    if (b.Items.Count + newCount > MaxItemsPerBlock) return $"One part can hold up to {MaxItemsPerBlock} items. Use another part for the rest.";
                    break;
            }
        }
        return null;
    }

    /// <summary>Checks a new upload's type and size for the kind of block it is going into.</summary>
    public static string? ValidateUpload(string blockType, string fileName, long length)
    {
        if (length <= 0) return $"\"{fileName}\" is empty.";
        if (blockType == EventUpdateBlockTypes.Images)
        {
            if (!IsImage(fileName)) return $"\"{fileName}\" isn't a photo. Use JPG, PNG, WebP or GIF, or put it in a files part.";
            if (length > MaxImageBytes) return $"\"{fileName}\" is bigger than {MaxImageBytes / 1024 / 1024} MB.";
            return null;
        }
        if (!IsDocument(fileName)) return $"\"{fileName}\" can't be attached. Allowed: PDF, Word, Excel, PowerPoint, text, CSV, RTF and ZIP.";
        if (length > MaxFileBytes) return $"\"{fileName}\" is bigger than {MaxFileBytes / 1024 / 1024} MB.";
        return null;
    }

    public static string? CleanCategory(string? category, out string cleaned)
    {
        cleaned = (category ?? "").Trim();
        if (cleaned.Length == 0) return "Choose a category for this update.";
        if (cleaned.Length > MaxCategoryLength) return $"A category can be up to {MaxCategoryLength} characters.";
        // A typed label that is just a standard one in other letters becomes the standard one, so the filters don't split.
        var typed = cleaned;
        cleaned = Categories.FirstOrDefault(c => c.Equals(typed, StringComparison.OrdinalIgnoreCase)) ?? typed;
        return null;
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
