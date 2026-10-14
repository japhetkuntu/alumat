using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ReservEase.Alumni.Member.Api.Controllers;
using ReservEase.Alumni.Member.Api.Services.Assistant;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using ReservEase.Alumni.Whatsapp.Sdk.Models;
using ReservEase.Alumni.Whatsapp.Sdk.Options;
using ReservEase.Alumni.Whatsapp.Sdk.Services;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

public class AssistantDirectoryTests
{
    private const string Phone = "233244123456";

    private static Institution Inst(string id, string slug, string name, string status = "Active", params string[] disabled)
        => new() { Id = id, Slug = slug, Name = name, PortalName = name, Status = status, DisabledFeatures = [.. disabled] };

    private static MemberEntity Person(string id, string inst, string phone = Phone, string status = "Active", DateTime? verified = null)
        => new() { Id = id, InstitutionId = inst, FirstName = "Ama", LastName = "M", Email = $"{id}@x.com", Phone = phone, Status = status, PhoneVerifiedAt = verified };

    private static AssistantDirectory Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name)) { foreach (var e in seed) db.Add(e); db.SaveChanges(); }
        var ctx = TestDb.Create(name);   // no tenant: every lookup must be explicit about crossing institutions
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PlatformBaseDomain"] = "alumunion.test" }).Build();
        return new AssistantDirectory(new AlumniPgRepository<MemberEntity>(ctx), new AlumniPgRepository<Institution>(ctx), config);
    }

    [Fact]
    public async Task A_number_finds_its_active_member_in_every_institution_and_says_which_are_verified()
    {
        var d = Create(Inst("i1", "umat", "UMaT"), Inst("i2", "pensa", "PENSA"),
            Person("a", "i1", verified: DateTime.UtcNow), Person("b", "i2"));

        var found = await d.FindMemberCommunitiesAsync(Phone);

        Assert.Equal(2, found.Count);
        Assert.True(found.Single(c => c.InstitutionId == "i1").Verified);
        Assert.False(found.Single(c => c.InstitutionId == "i2").Verified);
        Assert.Equal("https://umat.alumunion.test", found.Single(c => c.InstitutionId == "i1").PortalUrl);
        Assert.All(found, c => Assert.True(c.MemberId is not null));
    }

    [Fact]
    public async Task Pending_members_other_numbers_and_inactive_institutions_are_not_found()
    {
        var d = Create(Inst("i1", "umat", "UMaT"), Inst("i2", "gone", "Suspended Co", status: "Suspended"),
            Person("a", "i1", status: "Pending"), Person("b", "i1", phone: "233500000000"), Person("c", "i2"));

        Assert.Empty(await d.FindMemberCommunitiesAsync(Phone));
    }

    [Fact]
    public async Task When_one_number_is_on_two_rows_of_one_institution_the_verified_one_is_used()
    {
        var d = Create(Inst("i1", "umat", "UMaT"), Person("a", "i1"), Person("b", "i1", verified: DateTime.UtcNow));

        var only = Assert.Single(await d.FindMemberCommunitiesAsync(Phone));
        Assert.Equal("b", only.MemberId);
    }

    [Fact]
    public async Task Search_skips_an_institution_the_platform_has_deactivated()
    {
        var off = Inst("i1", "alpha", "Alpha Alumni"); off.WhatsAppBotEnabled = false;
        var d = Create(off, Inst("i2", "alphb", "Alpha Club"));

        var found = await d.SearchAsync("alpha");

        Assert.Equal("Alpha Club", Assert.Single(found).Name);
    }

    [Fact]
    public async Task Search_finds_by_name_or_code_skips_ones_with_the_assistant_off_and_ignores_very_short_text()
    {
        var d = Create(Inst("i1", "umat", "UMaT Alumni"), Inst("i2", "pensa", "PENSA UMaT"), Inst("i3", "off", "UMaT Staff", disabled: InstitutionFeatures.WhatsApp));

        var found = await d.SearchAsync("uma");
        Assert.Equal(["PENSA UMaT", "UMaT Alumni"], found.Select(f => f.Name).OrderBy(n => n));
        // An exact community code is unambiguous, so it wins over looser name matches.
        Assert.Equal("UMaT Alumni", Assert.Single(await d.SearchAsync("umat")).Name);
        Assert.Empty(await d.SearchAsync("um"));
        Assert.Equal("i1", (await d.FindBySlugAsync("UMAT"))!.InstitutionId);
        Assert.Null(await d.FindByIdAsync("missing"));
    }
}

public class WhatsAppWebhookControllerTests
{
    private const string Secret = "s3cret";

    private static readonly string Hi = """
        {"event":"messages-personal.received","timestamp":TS,"data":{"messages":{"key":{"id":"MSG1","fromMe":false,"remoteJid":"233244123456@s.whatsapp.net","cleanedSenderPn":"233244123456"},"messageBody":"hi"}}}
        """;

    private static (WhatsAppWebhookController controller, Mock<IWhatsAppProvider> provider) Create(string body, string? secret = Secret, bool enabled = true, bool botOn = true)
    {
        var provider = new Mock<IWhatsAppProvider>();
        provider.Setup(p => p.SendMessageAsync(It.IsAny<WhatsAppOutboundMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(WhatsAppSendResult.Sent());

        var directory = new Mock<IAssistantDirectory>();
        directory.Setup(d => d.FindMemberCommunitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var state = new Mock<IAssistantStateStore>();
        var engine = new AssistantEngine(directory.Object, Mock.Of<IAssistantContent>(), state.Object, Mock.Of<IAssistantLinker>(), Mock.Of<IAssistantActions>(), Options.Create(new WaSenderConfig()));

        var groups = new GroupCommandHandler(new AlumniPgRepository<Institution>(TestDb.Create()), directory.Object, Mock.Of<IAssistantContent>(), provider.Object,
            new InMemoryRedisService<ReservEase.Alumni.PaymentCallbacks.Sdk.Options.MemberRedisConfig>(), Options.Create(new WaSenderConfig()));

        var controller = new WhatsAppWebhookController(
            Options.Create(new WaSenderConfig { Enabled = enabled, WebhookSecret = Secret }), engine, groups, provider.Object,
            Mock.Of<ITemporalClientProvider>(t => t.IsAvailable == false), Mock.Of<ReservEase.Alumni.PostgresDb.Sdk.Services.IPlatformWhatsAppSwitch>(x => x.IsOnAsync() == Task.FromResult(botOn)),
            NullLogger<WhatsAppWebhookController>.Instance);

        var http = new DefaultHttpContext();
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        if (secret is not null) http.Request.Headers["X-Webhook-Signature"] = secret;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (controller, provider);
    }

    private static string Fresh() => Hi.Replace("TS", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());

    [Fact]
    public async Task When_the_platform_administrator_switches_the_bot_off_the_webhook_acknowledges_and_stays_silent()
    {
        var (controller, provider) = Create(Fresh(), botOn: false);

        var result = Assert.IsType<OkObjectResult>(await controller.Receive(CancellationToken.None));

        Assert.Contains("handled = False", result.Value!.ToString());
        provider.Verify(p => p.SendMessageAsync(It.IsAny<WhatsAppOutboundMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_wrong_or_missing_secret_is_rejected_and_nothing_is_sent()
    {
        foreach (var secret in new[] { "wrong", null })
        {
            var (c, provider) = Create(Fresh(), secret);
            Assert.IsType<UnauthorizedObjectResult>(await c.Receive(default));
            provider.Verify(p => p.SendMessageAsync(It.IsAny<WhatsAppOutboundMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Fact]
    public async Task While_the_channel_is_switched_off_the_webhook_is_acknowledged_and_ignored()
    {
        var (c, provider) = Create(Fresh(), enabled: false);
        Assert.IsType<OkObjectResult>(await c.Receive(default));
        provider.Verify(p => p.SendMessageAsync(It.IsAny<WhatsAppOutboundMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_real_message_is_answered_to_the_sender_even_when_the_workflow_engine_is_down()
    {
        var (c, provider) = Create(Fresh());

        Assert.IsType<OkObjectResult>(await c.Receive(default));

        provider.Verify(p => p.SendMessageAsync(
            It.Is<WhatsAppOutboundMessage>(m => m.To == "233244123456" && m.Text.Contains("Which community")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_message_that_is_far_too_old_is_not_answered()
    {
        var old = Hi.Replace("TS", DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds().ToString());
        var (c, provider) = Create(old);

        Assert.IsType<OkObjectResult>(await c.Receive(default));
        provider.Verify(p => p.SendMessageAsync(It.IsAny<WhatsAppOutboundMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("""{"event":"messages.reaction","data":{}}""")]
    [InlineData("not json at all")]
    public async Task Events_the_assistant_does_not_act_on_are_acknowledged(string body)
    {
        var (c, provider) = Create(body);
        Assert.IsType<OkObjectResult>(await c.Receive(default));
        provider.Verify(p => p.SendMessageAsync(It.IsAny<WhatsAppOutboundMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_sender_known_only_by_lid_is_resolved_to_a_number_first()
    {
        var body = """{"event":"messages-personal.received","timestamp":TS,"data":{"messages":{"key":{"id":"M2","remoteJid":"555@lid","senderLid":"555@lid"},"messageBody":"hi"}}}"""
            .Replace("TS", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
        var (c, provider) = Create(body);
        provider.Setup(p => p.ResolvePhoneFromLidAsync("555@lid", It.IsAny<CancellationToken>())).ReturnsAsync("233244123456");

        await c.Receive(default);

        provider.Verify(p => p.SendMessageAsync(It.Is<WhatsAppOutboundMessage>(m => m.To == "233244123456"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
