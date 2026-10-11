using ReservEase.Alumni.Common.Sdk.Services;

namespace ReservEase.Alumni.Common.Tests;

public class PhoneNumbersTests
{
    [Theory]
    [InlineData("0244123456", "233244123456")]
    [InlineData("024 412 3456", "233244123456")]
    [InlineData("024-412-3456", "233244123456")]
    [InlineData("+233244123456", "233244123456")]
    [InlineData("+233 24 412 3456", "233244123456")]
    [InlineData("233244123456", "233244123456")]
    [InlineData("00233244123456", "233244123456")]
    [InlineData("244123456", "233244123456")]
    [InlineData("+14155550123", "14155550123")]
    [InlineData("  +44 7700 900123 ", "447700900123")]
    public void The_same_number_in_any_spelling_reduces_to_one_form(string raw, string expected)
        => Assert.Equal(expected, PhoneNumbers.Normalize(raw));

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("   ")] [InlineData("abc")] [InlineData("12345")] [InlineData("1234567890123456")]
    public void Something_that_cannot_be_a_phone_number_is_nothing(string? raw)
        => Assert.Null(PhoneNumbers.Normalize(raw));

    [Fact]
    public void Two_spellings_are_the_same_number_and_different_numbers_are_not()
    {
        Assert.True(PhoneNumbers.AreSame("0244123456", "+233 244 123 456"));
        Assert.False(PhoneNumbers.AreSame("0244123456", "0244123457"));
        Assert.False(PhoneNumbers.AreSame(null, null));
    }

    [Fact]
    public void A_masked_number_can_be_recognised_but_not_dialled()
    {
        var masked = PhoneNumbers.Mask("233244123456");
        Assert.Equal("+233 24 •••• 456", masked);
        Assert.DoesNotContain("4123", masked);
        Assert.Equal("••••", PhoneNumbers.Mask(null));
    }
}
