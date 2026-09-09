using System.Globalization;

namespace ReservEase.Alumni.Mailtrap.Sdk.Services;

/// <summary>
/// Derives an email-safe color palette from a single brand hex using OKLCH
/// (a perceptually-uniform color space) rather than naive RGB/HSL mixing —
/// plain RGB interpolation makes "lighten by X%" look wildly different
/// across hues (yellow blows out, blue barely moves), which is exactly what
/// produced inconsistent-looking results before. Adjusting lightness in
/// OKLCH keeps perceived brightness consistent for any input hue.
///
/// Two guardrails on top of the color math, because "technically derived"
/// isn't the same as "looks good":
///  - Soft input clamping: a seed color that's too pale, too dark, or too
///    gray gets nudged into a workable band before anything is derived from
///    it, rather than faithfully reproducing a theme that was always going
///    to look bad.
///  - Contrast-checked text: <see cref="TextOn"/> picks white or near-black
///    text by actually computing WCAG contrast against the given
///    background, not by assuming white always works.
///
/// Every mail client (Outlook especially) needs plain hex values, not CSS
/// color functions, so everything here still resolves to a #rrggbb string.
/// </summary>
public static class EmailColorPalette
{
    // Clamp bounds tuned for "looks like a real UI accent color, not a
    // pastel wash or a near-black smudge" — same reasoning a hand-picked
    // brand palette would apply.
    private const double MinLightness = 0.38;
    private const double MaxLightness = 0.62;
    private const double MinChroma = 0.06;

    // A true neutral (pure white/black/gray) measures chroma essentially at
    // 0 in OKLCH. Anything above this tiny epsilon is a deliberate color
    // choice, even a subtle one — e.g. Tailwind's own "slate-200" (#E2E8F0)
    // is a common pick for a cool-toned secondary brand color and measures
    // ~0.013, which the old 0.02 cutoff wrongly treated as "no real color"
    // and silently flattened to flat gray.
    private const double AchromaticThreshold = 0.004;

    /// <summary>Hue (radians) of the platform's own default green (#0e7143) — used when a seed has no real hue to preserve (see <see cref="ClampSeed"/>).</summary>
    private static readonly double DefaultHue = ToOklch("#0e7143").h;

    // Mirrors MIN_ACCENT_HUE_SEPARATION_DEG in brand-palette.ts — below this
    // hue separation, primary and secondary render close enough to the same
    // color that an "accent" derived from secondary would just look like
    // primary again, not a real second color.
    private const double MinAccentHueSeparationDeg = 20.0;

    /// <summary>True when <paramref name="secondaryHex"/> is far enough in hue from <paramref name="primaryHex"/> to read as a real second color, not just a lighter/darker shade of the same one.</summary>
    public static bool IsDistinctAccent(string primaryHex, string secondaryHex)
    {
        var primaryHue = ToOklch(ClampSeed(primaryHex)).h;
        var secondaryHue = ToOklch(ClampSeed(secondaryHex)).h;
        return HueDistanceDeg(primaryHue, secondaryHue) >= MinAccentHueSeparationDeg;
    }

    private static double HueDistanceDeg(double h1Radians, double h2Radians)
    {
        double ToDeg(double r) => ((r * 180.0 / Math.PI) % 360.0 + 360.0) % 360.0;
        var diff = Math.Abs(ToDeg(h1Radians) - ToDeg(h2Radians)) % 360.0;
        return diff > 180.0 ? 360.0 - diff : diff;
    }

    public static string Dark(string hex) => WithLightness(hex, l => Math.Max(0.0, l - 0.16));
    public static string Light(string hex) => WithLightness(hex, _ => 0.94, chromaScale: 0.35);
    public static string Soft(string hex) => WithLightness(hex, _ => 0.86, chromaScale: 0.55);

    /// <summary>White or near-black — whichever has sufficient WCAG contrast against <paramref name="backgroundHex"/>.</summary>
    public static string TextOn(string backgroundHex)
    {
        var (r, g, b) = ParseHex(backgroundHex);
        var whiteContrast = ContrastRatio((r, g, b), (255, 255, 255));
        var blackContrast = ContrastRatio((r, g, b), (17, 24, 39)); // slate-900, not pure black — softer on light UIs too
        return whiteContrast >= blackContrast ? "#ffffff" : "#111827";
    }

    /// <summary>
    /// A picked color with any real chroma in it is used exactly as picked —
    /// admins expect the color they chose to be the color they see, not a
    /// "corrected" one. This used to also rebalance lightness into a fixed
    /// band, which pushed light, saturated seeds (e.g. a bright cyan) out of
    /// the sRGB gamut; the OKLCH-to-RGB conversion then silently
    /// channel-clips out-of-gamut values, which shifts the visible hue
    /// toward whatever channel got clipped — a light cyan clamped darker
    /// could come back out looking blue. Only a genuinely achromatic seed
    /// (white/black/gray — hue is meaningless there, and numerically noisy,
    /// atan2 near the origin) still falls back to the platform's own hue.
    /// </summary>
    public static string ClampSeed(string hex)
    {
        var (l, c, h) = ToOklch(hex);
        if (c >= AchromaticThreshold)
            return NormalizeHex(hex);

        var clampedL = Math.Clamp(l, MinLightness, MaxLightness);
        return FromOklch(clampedL, MinChroma, DefaultHue);
    }

    private static string NormalizeHex(string hex)
    {
        var (r, g, b) = ParseHex(hex);
        return $"#{r:x2}{g:x2}{b:x2}";
    }

    /// <summary>
    /// A version of <paramref name="hex"/> guaranteed to be legible as TEXT
    /// on <paramref name="backgroundHex"/> — walks lightness toward the
    /// background's opposite extreme (darker for a light background,
    /// lighter for a dark one) until WCAG contrast reaches
    /// <paramref name="minContrast"/>, tapering chroma slightly along the
    /// way so the result doesn't look neon, but keeping the original hue so
    /// the text still reads as "that brand color" rather than generic gray.
    /// Needed because <see cref="ClampSeed"/> no longer forces every picked
    /// color into a mid-lightness band before use — an admin-picked color
    /// that's very light or very dark would otherwise render as invisible,
    /// or near-invisible, text.
    /// </summary>
    public static string TextSafeOn(string hex, string backgroundHex, double minContrast = 4.5)
    {
        var (l, c, h) = ToOklch(hex);
        var bg = ParseHex(backgroundHex);
        var darkening = RelativeLuminance(bg) > 0.4;
        var testL = l;
        var chroma = c;
        for (var i = 0; i < 60; i++)
        {
            var candidate = FromOklch(testL, chroma, h);
            if (ContrastRatio(ParseHex(candidate), bg) >= minContrast)
                return candidate;
            testL += darkening ? -0.015 : 0.015;
            chroma *= 0.985;
            if (testL <= 0.02 || testL >= 0.98) break;
        }
        return darkening ? "#111827" : "#f8fafc";
    }

    private static string WithLightness(string hex, Func<double, double> adjustLightness, double chromaScale = 1.0)
    {
        var (l, c, h) = ToOklch(ClampSeed(hex));
        return FromOklch(adjustLightness(l), c * chromaScale, h);
    }

    // ── OKLCH conversion (Björn Ottosson's OKLab: https://bottosson.github.io/posts/oklab/) ──

    private static (double l, double c, double h) ToOklch(string hex)
    {
        var (r, g, b) = ParseHex(hex);
        var (L, a, bb) = LinearSrgbToOklab(SrgbToLinear(r / 255.0), SrgbToLinear(g / 255.0), SrgbToLinear(b / 255.0));
        var c = Math.Sqrt(a * a + bb * bb);
        var h = Math.Atan2(bb, a);
        return (L, c, h);
    }

    private static string FromOklch(double l, double c, double h)
    {
        var a = c * Math.Cos(h);
        var bb = c * Math.Sin(h);
        var (r, g, b) = OklabToLinearSrgb(l, a, bb);
        return $"#{ToByte(LinearToSrgb(r)):x2}{ToByte(LinearToSrgb(g)):x2}{ToByte(LinearToSrgb(b)):x2}";
    }

    private static (double L, double a, double b) LinearSrgbToOklab(double r, double g, double b)
    {
        var l = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b;
        var m = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b;
        var s = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b;

        var l_ = Cbrt(l);
        var m_ = Cbrt(m);
        var s_ = Cbrt(s);

        return (
            0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_,
            1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_,
            0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_
        );
    }

    private static (double r, double g, double b) OklabToLinearSrgb(double L, double a, double b)
    {
        var l_ = L + 0.3963377774 * a + 0.2158037573 * b;
        var m_ = L - 0.1055613458 * a - 0.0638541728 * b;
        var s_ = L - 0.0894841775 * a - 1.2914855480 * b;

        var l = l_ * l_ * l_;
        var m = m_ * m_ * m_;
        var s = s_ * s_ * s_;

        return (
            +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
            -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
            -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s
        );
    }

    private static double SrgbToLinear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    private static double LinearToSrgb(double c) => c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(Math.Clamp(c, 0, 1), 1 / 2.4) - 0.055;
    private static double Cbrt(double x) => Math.Sign(x) * Math.Pow(Math.Abs(x), 1.0 / 3.0);
    private static int ToByte(double c) => Math.Clamp((int)Math.Round(c * 255), 0, 255);

    // ── WCAG contrast (relative luminance, https://www.w3.org/TR/WCAG21/#dfn-relative-luminance) ──

    private static double ContrastRatio((int r, int g, int b) a, (int r, int g, int b) b)
    {
        var l1 = RelativeLuminance(a);
        var l2 = RelativeLuminance(b);
        var (lighter, darker) = l1 > l2 ? (l1, l2) : (l2, l1);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance((int r, int g, int b) c)
    {
        double Channel(int v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(c.r) + 0.7152 * Channel(c.g) + 0.0722 * Channel(c.b);
    }

    private static (int r, int g, int b) ParseHex(string hex)
    {
        hex = hex.Trim().TrimStart('#');
        if (hex.Length == 3)
            hex = string.Concat(hex.Select(ch => new string(ch, 2)));

        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            return (14, 113, 67); // fallback: the platform's own default green

        var r = int.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (r, g, b);
    }
}
