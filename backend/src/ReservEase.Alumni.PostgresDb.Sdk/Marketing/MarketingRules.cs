using System.Security.Cryptography;
using System.Text.Json;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
namespace ReservEase.Alumni.PostgresDb.Sdk.Marketing;
public record MarketingSnapshot(string CampaignTitle, string Title, string Content, string DestinationPath, bool UseLandingPage, List<MarketingAsset> Assets);
public static class MarketingRules
{
    public static readonly string[] Channels = ["Facebook", "Instagram", "LinkedIn", "WhatsApp", "X", "TikTok", "YouTube", "Other"];
    public static readonly string[] Statuses = ["Draft", "Ready", "Archived"];
    // Only our own marketing destinations, never an arbitrary redirect or login route.
    public static bool ValidDestination(string path) => path is "/" or "/#onboard" or "/#features" or "/#product" or "/#costs" or "/why-not-whatsapp";
    public static bool ValidPublishedUrl(string? url) => string.IsNullOrWhiteSpace(url) || (Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == "https" && string.IsNullOrEmpty(u.UserInfo));
    public static MarketingSnapshot Snapshot(MarketingShare share) => JsonSerializer.Deserialize<MarketingSnapshot>(share.SnapshotJson)!;

    // Excludes visually ambiguous characters (0/O, 1/l/I) so a code read aloud or
    // hand-typed from a printed poster doesn't get miskeyed.
    private const string ShortCodeAlphabet = "23456789abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ";
    public static string GenerateShortCode(int length = 7)
    {
        Span<char> code = stackalloc char[length];
        for (var i = 0; i < length; i++)
            code[i] = ShortCodeAlphabet[RandomNumberGenerator.GetInt32(ShortCodeAlphabet.Length)];
        return new string(code);
    }
}
