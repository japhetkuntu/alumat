using System.Text.RegularExpressions;
using ReservEase.Alumni.Mailtrap.Sdk.Services;

namespace ReservEase.Alumni.Mailtrap.Tests;

public class EmailColorPaletteTests
{
    private static readonly Regex Hex = new("^#[0-9a-f]{6}$");

    private static double Luminance(string hex)
    {
        double Ch(int i) { var s = Convert.ToInt32(hex.Substring(1 + i * 2, 2), 16) / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * Ch(0) + 0.7152 * Ch(1) + 0.0722 * Ch(2);
    }

    private static double Contrast(string a, string b)
    {
        var (hi, lo) = Luminance(a) > Luminance(b) ? (Luminance(a), Luminance(b)) : (Luminance(b), Luminance(a));
        return (hi + 0.05) / (lo + 0.05);
    }

    public static IEnumerable<object[]> Seeds() => new[] { "#2563eb", "#0e7143", "#ff0000", "#00ffff", "#ffff00", "#7c3aed", "#e2e8f0", "#111111", "#ffffff", "#000000", "#808080" }
        .Select(s => new object[] { s });

    [Theory, MemberData(nameof(Seeds))]
    public void Every_derived_shade_is_a_plain_hex_string(string seed)
    {
        Assert.Matches(Hex, EmailColorPalette.ClampSeed(seed));
        Assert.Matches(Hex, EmailColorPalette.Dark(seed));
        Assert.Matches(Hex, EmailColorPalette.Light(seed));
        Assert.Matches(Hex, EmailColorPalette.Soft(seed));
        Assert.Matches(Hex, EmailColorPalette.TextOn(seed));
        Assert.Matches(Hex, EmailColorPalette.TextSafeOn(seed, "#ffffff"));
    }

    [Theory]
    [InlineData("#2563eb")]
    [InlineData("#0e7143")]
    [InlineData("#e2e8f0")]
    public void A_chromatic_seed_is_used_exactly_as_picked(string seed)
        => Assert.Equal(seed, EmailColorPalette.ClampSeed(seed));

    [Fact]
    public void Seed_is_normalised_to_lowercase_six_digit_hex()
    {
        Assert.Equal("#2563eb", EmailColorPalette.ClampSeed("2563EB"));
        Assert.Equal("#aabbcc", EmailColorPalette.ClampSeed("#ABC"));
        Assert.Equal("#aabbcc", EmailColorPalette.ClampSeed("  #abc  "));
    }

    [Theory]
    [InlineData("#ffffff")]
    [InlineData("#000000")]
    [InlineData("#808080")]
    public void An_achromatic_seed_gets_the_platform_hue_with_a_workable_chroma(string seed)
    {
        var clamped = EmailColorPalette.ClampSeed(seed);
        Assert.NotEqual(seed, clamped);
        // blue-ish: blue channel dominates the platform default hue
        var r = Convert.ToInt32(clamped.Substring(1, 2), 16);
        var b = Convert.ToInt32(clamped.Substring(5, 2), 16);
        Assert.True(b > r);
    }

    [Fact]
    public void Garbage_hex_falls_back_to_the_default_green_instead_of_throwing()
    {
        var result = EmailColorPalette.ClampSeed("not-a-colour");
        Assert.Matches(Hex, result);
        Assert.Equal("#0e7143", result);
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Dark_is_darker_than_the_seed_and_Light_is_lighter_than_Soft(string seed)
    {
        Assert.True(Luminance(EmailColorPalette.Dark(seed)) <= Luminance(EmailColorPalette.ClampSeed(seed)) + 1e-9);
        Assert.True(Luminance(EmailColorPalette.Light(seed)) > Luminance(EmailColorPalette.Soft(seed)));
    }

    [Theory]
    [InlineData("#ffffff", "#111827")]
    [InlineData("#fde047", "#111827")]
    [InlineData("#000000", "#ffffff")]
    [InlineData("#1e3a8a", "#ffffff")]
    [InlineData("#0e7143", "#ffffff")]
    public void TextOn_picks_whichever_of_white_or_slate_has_more_contrast(string background, string expected)
        => Assert.Equal(expected, EmailColorPalette.TextOn(background));

    [Theory, MemberData(nameof(Seeds))]
    public void TextOn_always_yields_at_least_AA_large_text_contrast(string background)
        => Assert.True(Contrast(background, EmailColorPalette.TextOn(background)) >= 3.0);

    [Theory]
    [InlineData("#ffff00")]
    [InlineData("#00ffff")]
    [InlineData("#e2e8f0")]
    [InlineData("#2563eb")]
    [InlineData("#0e7143")]
    public void TextSafeOn_white_meets_4_5_contrast(string seed)
        => Assert.True(Contrast(EmailColorPalette.TextSafeOn(seed, "#ffffff"), "#ffffff") >= 4.5);

    [Fact]
    public void TextSafeOn_a_dark_background_lightens_instead_of_darkening()
    {
        var result = EmailColorPalette.TextSafeOn("#1e3a8a", "#0b1020");
        Assert.True(Luminance(result) > Luminance("#1e3a8a"));
        Assert.True(Contrast(result, "#0b1020") >= 4.5);
    }

    [Fact]
    public void TextSafeOn_returns_the_seed_when_it_already_has_enough_contrast()
        => Assert.Equal("#111827", EmailColorPalette.TextSafeOn("#111827", "#ffffff"));

    [Fact]
    public void TextSafeOn_honours_a_custom_minimum_contrast()
    {
        var lenient = EmailColorPalette.TextSafeOn("#7aa2f7", "#ffffff", 2.0);
        var strict = EmailColorPalette.TextSafeOn("#7aa2f7", "#ffffff", 7.0);
        Assert.True(Contrast(strict, "#ffffff") >= 7.0);
        Assert.True(Luminance(strict) < Luminance(lenient));
    }

    [Theory]
    [InlineData("#2563eb", "#dc2626", true)]   // blue vs red
    [InlineData("#2563eb", "#16a34a", true)]   // blue vs green
    [InlineData("#2563eb", "#3b82f6", false)]  // two blues
    [InlineData("#2563eb", "#2563eb", false)]
    public void IsDistinctAccent_requires_a_real_hue_difference(string primary, string secondary, bool expected)
        => Assert.Equal(expected, EmailColorPalette.IsDistinctAccent(primary, secondary));

    [Fact]
    public void IsDistinctAccent_is_symmetric_and_handles_hue_wraparound()
    {
        // hues just either side of 0/360 degrees are close, not 340 apart
        Assert.False(EmailColorPalette.IsDistinctAccent("#ff0010", "#ff1000"));
        Assert.Equal(EmailColorPalette.IsDistinctAccent("#2563eb", "#dc2626"), EmailColorPalette.IsDistinctAccent("#dc2626", "#2563eb"));
    }

    [Fact]
    public void Derivations_are_deterministic()
        => Assert.Equal(EmailColorPalette.Dark("#7c3aed"), EmailColorPalette.Dark("#7c3aed"));
}
