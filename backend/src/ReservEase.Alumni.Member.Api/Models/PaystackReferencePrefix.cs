namespace ReservEase.Alumni.Member.Api.Models;

/// <summary>
/// Stamps a short, fixed prefix onto every Paystack reference at initiation so the
/// webhook handler can tell which service owns a callback by parsing the string —
/// no DB lookups needed. A reference with no recognized prefix predates this scheme
/// (in-flight at deploy time) and must fall back to the old existence-check routing.
/// </summary>
public static class PaystackReferencePrefix
{
    public const string StoreOrder = "SO";
    public const string ServiceRequest = "SR";
    public const string Contribution = "CN";

    public static string NewReference(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    public static string? ExtractPrefix(string reference)
    {
        var separatorIndex = reference.IndexOf('_');
        return separatorIndex > 0 ? reference[..separatorIndex] : null;
    }
}
