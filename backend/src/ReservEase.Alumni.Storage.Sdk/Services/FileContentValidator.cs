using Microsoft.AspNetCore.Http;

namespace ReservEase.Alumni.Storage.Sdk.Services;

/// <summary>
/// Every upload endpoint across the three APIs used to trust the client-supplied
/// `Content-Type` header alone (trivially spoofable via the multipart request)
/// to decide whether a file was really an image — so any file could be relabeled
/// "image/png" and stored with a public-read ACL. This sniffs the actual bytes
/// for the declared type's real magic number, the same way a browser or `file`
/// itself would, rather than trusting what the uploader claims.
///
/// SVG is deliberately excluded from "image" entirely (see UploadService call
/// sites) rather than magic-byte-checked here — it's plain XML with no fixed
/// binary signature to verify, and it can embed &lt;script&gt;, so allowing it
/// as a publicly-served "image" is a stored-XSS vector regardless of how
/// faithfully its Content-Type is checked.
/// </summary>
public static class FileContentValidator
{
    /// <summary>Reads just enough of the stream to check the magic number, then rewinds it — safe to call before the caller's own upload/copy of the same file.</summary>
    public static async Task<bool> LooksLikeDeclaredImageTypeAsync(IFormFile file)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length));
        stream.Position = 0;
        if (read < 4) return false;

        return file.ContentType.ToLowerInvariant() switch
        {
            "image/png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
            "image/jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            "image/gif" => header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38,
            "image/webp" => read >= 12
                && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,
            _ => false,
        };
    }
}
