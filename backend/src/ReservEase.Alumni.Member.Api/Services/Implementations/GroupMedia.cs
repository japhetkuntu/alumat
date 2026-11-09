using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

/// <summary>
/// The pictures and links a leader can attach to what they post. Pictures are uploaded here, on the server, after checking what the file
/// really is (not just what it is called), so a post can only ever point at a picture this platform stored. Links are only ever web
/// addresses, and videos only YouTube.
/// </summary>
public class GroupMedia(IStorageService storage, ICurrentTenantService currentTenant)
{
    public const int MaxGallery = 8;
    public const int MaxVideos = 5;
    public const long MaxImageBytes = 5 * 1024 * 1024;
    private const string Folder = "alumni";

    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private static readonly HashSet<string> YoutubeHosts = new(StringComparer.OrdinalIgnoreCase) { "youtube.com", "www.youtube.com", "m.youtube.com", "youtu.be", "www.youtu.be" };

    /// <summary>Why a picture is not acceptable, or null when it is. Checks the size, the name's extension and the file's own first bytes.</summary>
    public static async Task<string?> CheckImageAsync(IFormFile file)
    {
        if (file.Length == 0) return "One of the pictures is empty";
        if (file.Length > MaxImageBytes) return "Pictures can be up to 5 MB each";
        if (!Extensions.Contains(Path.GetExtension(file.FileName))) return "Pictures must be JPEG, PNG, GIF or WebP";

        var head = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(head);
        return LooksLikeImage(head.AsSpan(0, read)) ? null : "That file isn't a picture we can use";
    }

    private static bool LooksLikeImage(ReadOnlySpan<byte> b)
        => (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)                                                    // JPEG
        || (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)                                    // PNG
        || (b.Length >= 4 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x38)                                    // GIF
        || (b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50); // WebP

    /// <summary>Checks a banner and a gallery together, before anything is stored, so a bad file never leaves half a post behind.</summary>
    public static async Task<string?> CheckAsync(IFormFile? banner, IReadOnlyCollection<IFormFile>? gallery, int keptInGallery = 0)
    {
        if (gallery is not null && gallery.Count + keptInGallery > MaxGallery) return $"Use up to {MaxGallery} pictures";
        if (banner is not null && await CheckImageAsync(banner) is { } bannerProblem) return bannerProblem;
        foreach (var f in gallery ?? []) if (await CheckImageAsync(f) is { } problem) return problem;
        return null;
    }

    public Task<string> UploadAsync(IFormFile file)
        => storage.UploadFileAsync(file, $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}", Folder, currentTenant.InstitutionSlug ?? "");

    public async Task<List<string>> UploadAllAsync(IEnumerable<IFormFile>? files)
    {
        var urls = new List<string>();
        foreach (var f in files ?? []) urls.Add(await UploadAsync(f));
        return urls;
    }

    /// <summary>
    /// What a post keeps when it is edited: only pictures it already had (a request cannot attach some other address), minus any removed,
    /// then whatever was newly uploaded.
    /// </summary>
    public static List<string> Keep(IEnumerable<string>? had, IEnumerable<string>? asked, IEnumerable<string> added)
        => (asked ?? []).Where(u => (had ?? []).Contains(u)).Distinct().Concat(added).ToList();

    /// <summary>A web address (http or https) for a map, a meeting or an apply button; null for none, and a message for something else.</summary>
    public static string? CleanWebLink(string? url, out string? clean)
    {
        clean = null;
        if (string.IsNullOrWhiteSpace(url)) return null;
        var t = url.Trim();
        if (t.Length > 500 || !Uri.TryCreate(t, UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttps && u.Scheme != Uri.UriSchemeHttp))
            return "Links must be web addresses starting with https://";
        clean = t;
        return null;
    }

    /// <summary>YouTube links only, a handful at most.</summary>
    public static string? CleanVideos(IEnumerable<string>? urls, out List<string>? clean)
    {
        clean = null;
        var list = (urls ?? []).Select(u => u?.Trim() ?? "").Where(u => u.Length > 0).Distinct().ToList();
        if (list.Count == 0) return null;
        if (list.Count > MaxVideos) return $"Use up to {MaxVideos} videos";
        foreach (var u in list)
            if (!Uri.TryCreate(u, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !YoutubeHosts.Contains(uri.Host))
                return "Videos must be YouTube links";
        clean = list;
        return null;
    }
}
