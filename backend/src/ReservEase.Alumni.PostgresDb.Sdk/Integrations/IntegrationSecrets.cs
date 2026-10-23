using System.Security.Cryptography;
using System.Text;

namespace ReservEase.Alumni.PostgresDb.Sdk.Integrations;

/// <summary>Makes and hashes the random values behind partner API keys and registration links. Only hashes are ever stored.</summary>
public static class IntegrationSecrets
{
    public const string ApiKeyMarker = "alk_";
    private const int PrefixLength = 10;

    /// <summary>A new API key (256 random bits), its short display prefix and the hash to store.</summary>
    public static (string Key, string Prefix, string Hash) NewApiKey()
    {
        var key = ApiKeyMarker + Base64Url(RandomNumberGenerator.GetBytes(32));
        return (key, key[..PrefixLength], Hash(key));
    }

    /// <summary>A new registration-link token (256 random bits) and the hash to store.</summary>
    public static (string Token, string Hash) NewLinkToken()
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    /// <summary>SHA-256, lower-case hex. The values hashed are long and random, so no salt or slow hash is needed, and a lookup by hash stays one indexed read.</summary>
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    /// <summary>A student ID as compared: trimmed. Null when empty or longer than any real ID.</summary>
    public static string? NormaliseStudentId(string? id)
    {
        var trimmed = id?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > 64 ? null : trimmed;
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
