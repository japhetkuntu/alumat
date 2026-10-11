namespace ReservEase.Alumni.Common.Sdk.Services;

/// <summary>
/// One way to compare phone numbers. Members type them as "024 412 3456", "0244123456", "+233244123456" or "00233…", and
/// WhatsApp reports them as bare digits with the country code. Everything is reduced to digits with the country code so two
/// spellings of the same number compare equal. A local number (leading 0, or the 9 national digits) is read as Ghanaian, the
/// platform's home market; anything already carrying another country code is kept as given.
/// </summary>
public static class PhoneNumbers
{
    public const string DefaultCountryCode = "233";

    /// <summary>Digits with country code (e.g. "233244123456"), or null when the input is empty or cannot be a phone number.</summary>
    public static string? Normalize(string? raw, string defaultCountryCode = DefaultCountryCode)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var hadPlus = raw.TrimStart().StartsWith('+');
        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 0) return null;

        if (!hadPlus && digits.StartsWith("00")) digits = digits[2..];
        else if (!hadPlus && digits.StartsWith('0') && digits.Length is >= 9 and <= 11) digits = defaultCountryCode + digits[1..];
        else if (!hadPlus && !digits.StartsWith(defaultCountryCode) && digits.Length == 9) digits = defaultCountryCode + digits;

        return digits.Length is >= 8 and <= 15 ? digits : null;
    }

    /// <summary>The same person's number written two ways.</summary>
    public static bool AreSame(string? a, string? b)
    {
        var x = Normalize(a);
        return x is not null && x == Normalize(b);
    }

    /// <summary>"+233 24 •••• 456": enough to recognise, never enough to dial.</summary>
    public static string Mask(string? normalized)
    {
        if (string.IsNullOrEmpty(normalized) || normalized.Length < 7) return "••••";
        return $"+{normalized[..3]} {normalized.Substring(3, 2)} •••• {normalized[^3..]}";
    }
}
