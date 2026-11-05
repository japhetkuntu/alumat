using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ReservEase.Alumni.Member.Api.Services.Security;

/// <summary>Where the keys for private conversations live: the server's own settings, never the database or the code.</summary>
public sealed class MessageEncryptionOptions
{
    public const string Section = "MessageEncryption";
    /// <summary>The key new text is encrypted with. Older keys stay listed so what they protected can still be read.</summary>
    public string CurrentKeyId { get; set; } = string.Empty;
    /// <summary>Key id to a 32-byte key, base64 encoded. Generate one with <c>openssl rand -base64 32</c>.</summary>
    public Dictionary<string, string> Keys { get; set; } = new();
}

/// <summary>Encrypts the text of private conversations before it is stored, and reads it back for the people in them.</summary>
public interface IMessageProtector
{
    /// <summary>Encrypts <paramref name="plaintext"/>. <paramref name="context"/> ties the result to one row and field, so it cannot be moved to another.</summary>
    string Protect(string plaintext, string context);
    /// <summary>Reads text written by <see cref="Protect"/>. Text stored before encryption existed is returned as it is. Throws <see cref="CryptographicException"/> if it was altered or the context differs.</summary>
    string Unprotect(string stored, string context);
    bool IsProtected(string? stored);
}

/// <summary>
/// AES-256-GCM. Each value gets its own random nonce, so equal messages never look equal in the database, and GCM detects any change.
/// Stored form: <c>v1.{keyId}.{base64url(nonce + ciphertext + tag)}</c>. This is encryption at rest: the server can read the text
/// (it must, to show it and to handle a report), but a copy of the database without the key is unreadable.
/// </summary>
public sealed partial class AesGcmMessageProtector : IMessageProtector
{
    private const int NonceSize = 12, TagSize = 16, KeySize = 32;
    private readonly string _currentId;
    private readonly Dictionary<string, byte[]> _keys = new();

    public AesGcmMessageProtector(MessageEncryptionOptions options)
    {
        foreach (var (id, b64) in options.Keys)
        {
            if (!KeyId().IsMatch(id)) throw new InvalidOperationException($"Message encryption key id “{id}” may only use letters, digits, dashes and underscores.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(b64); }
            catch (FormatException) { throw new InvalidOperationException($"Message encryption key “{id}” is not valid base64."); }
            if (bytes.Length != KeySize) throw new InvalidOperationException($"Message encryption key “{id}” must be {KeySize} bytes (generate one with: openssl rand -base64 32).");
            _keys[id] = bytes;
        }
        if (string.IsNullOrWhiteSpace(options.CurrentKeyId) || !_keys.ContainsKey(options.CurrentKeyId))
            throw new InvalidOperationException("MessageEncryption:CurrentKeyId must name one of MessageEncryption:Keys.");
        _currentId = options.CurrentKeyId;
    }

    public bool IsProtected(string? stored) => stored is not null && Stored().IsMatch(stored);

    public string Protect(string plaintext, string context)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var data = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[data.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(_keys[_currentId], TagSize)) aes.Encrypt(nonce, data, cipher, tag, Encoding.UTF8.GetBytes(context));
        var all = new byte[NonceSize + cipher.Length + TagSize];
        nonce.CopyTo(all, 0); cipher.CopyTo(all, NonceSize); tag.CopyTo(all, NonceSize + cipher.Length);
        return $"v1.{_currentId}.{Convert.ToBase64String(all).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
    }

    public string Unprotect(string stored, string context)
    {
        if (!IsProtected(stored)) return stored;   // written before encryption existed
        var parts = stored.Split('.', 3);
        if (!_keys.TryGetValue(parts[1], out var key)) throw new CryptographicException($"Unknown message key “{parts[1]}”.");
        var b64 = parts[2].Replace('-', '+').Replace('_', '/');
        var all = Convert.FromBase64String(b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '='));
        if (all.Length < NonceSize + TagSize) throw new CryptographicException("Stored message is too short.");
        var nonce = all.AsSpan(0, NonceSize);
        var cipher = all.AsSpan(NonceSize, all.Length - NonceSize - TagSize);
        var tag = all.AsSpan(all.Length - TagSize, TagSize);
        var plain = new byte[cipher.Length];
        using (var aes = new AesGcm(key, TagSize)) aes.Decrypt(nonce, cipher, tag, plain, Encoding.UTF8.GetBytes(context));
        return Encoding.UTF8.GetString(plain);
    }

    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,32}$")] private static partial Regex KeyId();
    [GeneratedRegex(@"^v1\.[A-Za-z0-9_-]{1,32}\.[A-Za-z0-9_-]{28,}$")] private static partial Regex Stored();
}
