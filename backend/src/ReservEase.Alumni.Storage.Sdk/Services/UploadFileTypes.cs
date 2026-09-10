namespace ReservEase.Alumni.Storage.Sdk.Services;

/// <summary>
/// Single source of truth for "which content-types can this platform store,
/// and what extension does each get" — shared by Institution/Member/Platform
/// Api's upload services so the mapping can't quietly diverge between them
/// (adding a new image type used to mean editing three separate switch
/// expressions). The stored extension is always derived from this table,
/// never the client-supplied filename — see each UploadService call site.
/// </summary>
public static class UploadFileTypes
{
    /// <summary>SVG deliberately excluded — see FileContentValidator's doc comment.</summary>
    public static readonly IReadOnlyDictionary<string, string> ImageExtensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp",
    };

    /// <summary>Non-image types the generic (institution Resources) upload endpoint additionally accepts.</summary>
    public static readonly IReadOnlyDictionary<string, string> DocumentExtensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = ".pdf",
        ["application/msword"] = ".doc",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
        ["application/vnd.ms-excel"] = ".xls",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = ".xlsx",
        ["application/vnd.ms-powerpoint"] = ".ppt",
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = ".pptx",
        ["text/plain"] = ".txt",
        ["text/csv"] = ".csv",
        ["application/zip"] = ".zip",
        ["audio/mpeg"] = ".mp3",
        ["video/mp4"] = ".mp4",
    };

    public static string SafeExtension(string contentType) =>
        ImageExtensions.TryGetValue(contentType, out var imageExt) ? imageExt
        : DocumentExtensions.TryGetValue(contentType, out var docExt) ? docExt
        : ".bin";
}
