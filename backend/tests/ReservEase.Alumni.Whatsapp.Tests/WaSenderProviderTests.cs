using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReservEase.Alumni.TestKit;
using ReservEase.Alumni.Whatsapp.Sdk.Models;
using ReservEase.Alumni.Whatsapp.Sdk.Options;
using ReservEase.Alumni.Whatsapp.Sdk.Services;

namespace ReservEase.Alumni.Whatsapp.Tests;

public class WaSenderProviderTests
{
    private static WaSenderProvider Create(FakeHttpHandler handler) =>
        new(Options.Create(new WaSenderConfig { ApiKey = "secret", BaseUrl = "https://wa.test" }), new FakeHttpClientFactory(handler), NullLogger<WaSenderProvider>.Instance);

    [Fact]
    public async Task A_send_returns_the_provider_message_id_when_it_gives_one()
    {
        var handler = FakeHttpHandler.Json(HttpStatusCode.OK, "{\"success\":true,\"data\":{\"msgId\":\"ABC123\"}}");
        var r = await Create(handler).SendMessageAsync(new WhatsAppOutboundMessage("233244123456", "Hello"));
        Assert.Equal((true, "ABC123"), (r.Success, r.MessageId));
    }

    [Theory]
    [InlineData(429, true)] [InlineData(502, true)] [InlineData(503, true)] [InlineData(400, false)] [InlineData(401, false)] [InlineData(404, false)]
    public async Task A_failed_send_says_whether_trying_again_is_worthwhile(int status, bool retryable)
    {
        var r = await Create(FakeHttpHandler.Json((HttpStatusCode)status, "{}")).SendMessageAsync(new WhatsAppOutboundMessage("233244123456", "x"));
        Assert.Equal((false, retryable), (r.Success, r.Retryable));
    }

    [Fact]
    public async Task A_network_failure_is_a_retryable_result_not_an_exception()
    {
        var r = await Create(FakeHttpHandler.Throwing(new HttpRequestException("down"))).SendMessageAsync(new WhatsAppOutboundMessage("233244123456", "x"));
        Assert.Equal((false, true), (r.Success, r.Retryable));
    }

    [Fact]
    public async Task A_group_is_recognised_by_its_jid()
    {
        Assert.True(new WhatsAppOutboundMessage("1203630@g.us", "x").IsGroup);
        Assert.False(new WhatsAppOutboundMessage("233244123456", "x").IsGroup);
        var handler = FakeHttpHandler.Json(HttpStatusCode.OK);
        await Create(handler).SendMessageAsync(new WhatsAppOutboundMessage("1203630@g.us", "x"));
        Assert.Contains("\"to\":\"1203630@g.us\"", handler.Bodies[0]);
    }

    [Fact]
    public async Task Connection_status_reads_connected_and_reports_an_unreachable_provider()
    {
        var up = await Create(FakeHttpHandler.Json(HttpStatusCode.OK, "{\"status\":\"connected\"}")).GetConnectionStatusAsync();
        Assert.Equal((true, "connected"), (up.Connected, up.State));

        var down = await Create(FakeHttpHandler.Json(HttpStatusCode.BadGateway, "{}")).GetConnectionStatusAsync();
        Assert.Equal((false, "unreachable"), (down.Connected, down.State));
    }

    [Fact]
    public async Task A_group_lookup_returns_its_name_or_nothing()
    {
        var found = await Create(FakeHttpHandler.Json(HttpStatusCode.OK, "{\"data\":{\"subject\":\"UMaT Class of 2015\"}}")).GetGroupAsync("1203@g.us");
        Assert.Equal("UMaT Class of 2015", found!.Name);
        Assert.Null(await Create(FakeHttpHandler.Json(HttpStatusCode.NotFound, "{}")).GetGroupAsync("1203@g.us"));
    }

    [Fact]
    public async Task A_lid_resolves_to_digits_only()
    {
        var phone = await Create(FakeHttpHandler.Json(HttpStatusCode.OK, "{\"data\":{\"pn\":\"233244123456@s.whatsapp.net\"}}")).ResolvePhoneFromLidAsync("55@lid");
        Assert.Equal("233244123456", phone);
    }
}

public class WaSenderWebhookParserTests
{
    private const string Personal = """
        {"event":"messages-personal.received","timestamp":1633456789,"data":{"messages":{"key":{"id":"3EB0X1","fromMe":false,"remoteJid":"555@lid","addressingMode":"pn","senderPn":"233244123456@s.whatsapp.net","cleanedSenderPn":"233244123456","senderLid":"555@lid"},"messageBody":"Hi","message":{"conversation":"Hi"}}}}
        """;

    private const string Group = """
        {"event":"messages-group.received","timestamp":1633456790,"data":{"messages":{"key":{"id":"3EB0G1","fromMe":false,"remoteJid":"1203630@g.us","participant":"123@lid","cleanedParticipantPn":"233501234567"},"messageBody":"!events"}}}
        """;

    [Fact]
    public void A_private_message_gives_sender_text_and_id()
    {
        var e = WaSenderWebhookParser.Parse(Personal);
        Assert.Equal(WhatsAppInboundKind.PersonalMessage, e.Kind);
        Assert.Equal(("3EB0X1", "233244123456", "Hi", false), (e.MessageId, e.SenderPhone, e.Text, e.FromMe));
        Assert.Equal("233244123456", e.ReplyTo);
        Assert.Equal(1633456789, e.SentAt!.Value.ToUnixTimeSeconds());
    }

    [Fact]
    public void A_group_message_replies_to_the_group_and_names_the_participant()
    {
        var e = WaSenderWebhookParser.Parse(Group);
        Assert.Equal(WhatsAppInboundKind.GroupMessage, e.Kind);
        Assert.Equal(("1203630@g.us", "233501234567", "!events"), (e.ChatId, e.SenderPhone, e.Text));
        Assert.Equal("1203630@g.us", e.ReplyTo);
    }

    [Fact]
    public void A_sender_with_only_a_lid_has_no_phone_but_keeps_the_lid()
    {
        var e = WaSenderWebhookParser.Parse("""{"event":"messages-personal.received","data":{"messages":{"key":{"id":"x","remoteJid":"555@lid","senderLid":"555@lid"},"messageBody":"hi"}}}""");
        Assert.Equal((null, "555@lid"), (e.SenderPhone, e.SenderLid));
    }

    [Fact]
    public void Our_own_messages_are_marked_so_they_can_be_dropped()
        => Assert.True(WaSenderWebhookParser.Parse(Personal.Replace("\"fromMe\":false", "\"fromMe\":true")).FromMe);

    [Theory]
    [InlineData("{\"event\":\"messages.reaction\",\"data\":{}}")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"event\":\"messages-personal.received\",\"data\":{}}")]
    public void Anything_else_is_ignored_never_an_exception(string body)
        => Assert.Equal(WhatsAppInboundKind.Ignored, WaSenderWebhookParser.Parse(body).Kind);

    [Theory]
    [InlineData("1760221925000")]    // milliseconds, as WaSender really sends it
    [InlineData("1760221925")]       // seconds, as its documentation says
    [InlineData("\"1760221925000\"")] // a numeric string
    public void The_timestamp_is_read_whether_it_is_seconds_or_milliseconds(string value)
    {
        var e = WaSenderWebhookParser.Parse(WithTimestamp(value));

        Assert.Equal(WhatsAppInboundKind.PersonalMessage, e.Kind);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1760221925), e.SentAt);
    }

    [Theory]
    [InlineData("99999999999999999")]
    [InlineData("-5")]
    [InlineData("\"soon\"")]
    public void A_nonsense_timestamp_never_throws_and_just_means_no_time(string value)
    {
        var e = WaSenderWebhookParser.Parse(WithTimestamp(value));

        Assert.Equal(WhatsAppInboundKind.PersonalMessage, e.Kind);
        Assert.Null(e.SentAt);
        Assert.Equal("hi", e.Text);
    }

    private static string WithTimestamp(string value)
        => "{\"event\":\"messages-personal.received\",\"timestamp\":" + value + ",\"data\":{\"messages\":{\"key\":{\"id\":\"x\",\"remoteJid\":\"233244123456@s.whatsapp.net\",\"cleanedSenderPn\":\"233244123456\"},\"messageBody\":\"hi\"}}}";

    [Theory]
    [InlineData("{\"extendedTextMessage\":{\"text\":\"2\",\"contextInfo\":{\"quotedMessage\":{\"conversation\":\"menu\"}}}}", "2")]   // swiped "reply" to the menu
    [InlineData("{\"conversation\":\"1\"}", "1")]
    [InlineData("{\"imageMessage\":{\"caption\":\"3\"}}", "3")]
    [InlineData("{\"buttonsResponseMessage\":{\"selectedButtonId\":\"4\"}}", "4")]
    [InlineData("{\"listResponseMessage\":{\"singleSelectReply\":{\"selectedRowId\":\"5\"}}}", "5")]
    public void The_typed_words_are_found_wherever_whatsapp_put_them(string message, string expected)
    {
        var json = "{\"event\":\"messages-personal.received\",\"data\":{\"messages\":{\"key\":{\"id\":\"x\",\"remoteJid\":\"233244123456@s.whatsapp.net\",\"cleanedSenderPn\":\"233244123456\"},\"message\":" + message + "}}}";

        var e = WaSenderWebhookParser.Parse(json);

        Assert.Equal(expected, e.Text);
        Assert.Null(e.MediaKind);
    }

    [Theory]
    [InlineData("messages-personal.received", "120363@g.us", WhatsAppInboundKind.GroupMessage)]    // the address says group
    [InlineData("messages-group.received", "233244123456@s.whatsapp.net", WhatsAppInboundKind.PersonalMessage)]
    [InlineData("messages-group.received", "120363@g.us", WhatsAppInboundKind.GroupMessage)]
    public void Where_a_message_was_posted_is_taken_from_the_chat_address_not_the_event_name(string ev, string jid, WhatsAppInboundKind expected)
    {
        var json = "{\"event\":\"" + ev + "\",\"data\":{\"messages\":{\"key\":{\"id\":\"x\",\"remoteJid\":\"" + jid + "\",\"participant\":\"555@lid\",\"cleanedParticipantPn\":\"233244123456\",\"cleanedSenderPn\":\"233244123456\"},\"messageBody\":\"!link K7M2QX9P\"}}}";

        Assert.Equal(expected, WaSenderWebhookParser.Parse(json).Kind);
    }

    [Theory]
    [InlineData("audioMessage", "voice")]
    [InlineData("imageMessage", "image")]
    [InlineData("locationMessage", "location")]
    [InlineData("documentMessage", "document")]
    public void A_message_with_no_text_reports_only_what_kind_it_was(string key, string kind)
    {
        var json = "{\"event\":\"messages-personal.received\",\"data\":{\"messages\":{\"key\":{\"id\":\"x\",\"remoteJid\":\"233244123456@s.whatsapp.net\",\"cleanedSenderPn\":\"233244123456\"},\"message\":{\"" + key + "\":{\"url\":\"https://x\"}}}}}";

        var e = WaSenderWebhookParser.Parse(json);

        Assert.Equal(WhatsAppInboundKind.PersonalMessage, e.Kind);
        Assert.Equal(kind, e.MediaKind);
        Assert.Null(e.Text);
    }

    [Fact]
    public void A_text_message_has_no_media_kind() => Assert.Null(WaSenderWebhookParser.Parse(Personal).MediaKind);

    [Fact]
    public void A_session_status_event_carries_the_state()
    {
        var e = WaSenderWebhookParser.Parse("""{"event":"session.status","data":{"status":"DISCONNECTED"}}""");
        Assert.Equal((WhatsAppInboundKind.SessionStatus, "disconnected"), (e.Kind, e.SessionState));
    }
}

public class WaSenderWebhookAuthTests
{
    [Theory]
    [InlineData("s3cret", "s3cret", true)]
    [InlineData("s3cret", "other", false)]
    [InlineData("", "s3cret", false)]
    [InlineData(null, "s3cret", false)]
    [InlineData("s3cret", "", false)]
    [InlineData("", "", false)]
    public void Only_the_exact_configured_secret_passes_and_an_unset_secret_never_does(string? header, string secret, bool expected)
        => Assert.Equal(expected, WaSenderWebhookAuth.IsValid(header, secret));
}
