using ReservEase.Alumni.Institution.Api.Models;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>SMS and email broadcasts cost money, so each is off until the platform switches it on; in-app is always free.</summary>
public class BroadcastChannelRulesTests
{
    [Fact]
    public void In_app_is_never_blocked()
        => Assert.Empty(BroadcastChannelRules.Blocked(["InApp"], emailAllowed: false, smsAllowed: false, whatsAppAllowed: false));

    [Fact]
    public void A_channel_the_platform_has_not_switched_on_is_blocked_by_name()
    {
        var blocked = BroadcastChannelRules.Blocked(["InApp", "Sms", "Email"], emailAllowed: true, smsAllowed: false, whatsAppAllowed: false);
        Assert.Equal(["SMS"], blocked);
    }

    [Fact]
    public void Everything_is_allowed_once_the_platform_switches_it_on()
        => Assert.Empty(BroadcastChannelRules.Blocked(["InApp", "Sms", "Email", "WhatsApp"], true, true, true));

    [Fact]
    public void Channel_names_are_not_case_sensitive_so_a_lowercase_request_cannot_slip_through()
    {
        var blocked = BroadcastChannelRules.Blocked(["sms", "EMAIL", "whatsapp"], false, false, false);
        Assert.Equal(["SMS", "email", "WhatsApp"], blocked.OrderBy(x => x == "SMS" ? 0 : x == "email" ? 1 : 2));
    }

    [Fact]
    public void Blank_entries_and_duplicates_are_ignored()
        => Assert.Equal(["SMS"], BroadcastChannelRules.Blocked(["", " ", "Sms", "SMS"], true, false, true));
}
