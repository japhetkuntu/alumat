using Microsoft.Extensions.Options;
using Moq;
using ReservEase.Alumni.Member.Api.Services.Assistant;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using ReservEase.Alumni.Whatsapp.Sdk.Models;
using ReservEase.Alumni.Whatsapp.Sdk.Options;
using ReservEase.Alumni.Whatsapp.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Tests;

public class GroupCommandHandlerTests
{
    private const string Group = "120363000000@g.us";

    private static WhatsAppGroupLink Active(params string[] allowed) => new()
    {
        Id = "g1", Name = "Class of 2015", GroupJid = Group, Status = WhatsAppGroupStatuses.Active, AllowedCommands = [.. allowed],
    };

    private static WhatsAppGroupLink Waiting(string code = "K7M2QX9P", DateTime? expires = null) => new()
    {
        Id = "g2", Name = "Draft", Status = WhatsAppGroupStatuses.AwaitingGroup, Code = code, CodeExpiresAt = expires ?? DateTime.UtcNow.AddHours(1),
    };

    private static Institution Inst(string id = "i1", params WhatsAppGroupLink[] groups) => new()
    {
        Id = id, Slug = "umat", Name = "UMaT", PortalName = "UMaT Alumni", Status = "Active", WhatsAppGroups = [.. groups],
    };

    private sealed class Content : IAssistantContent
    {
        public HashSet<string> Disabled { get; } = [];
        private static Task<IReadOnlyList<AssistantItem>> Some(string t) => Task.FromResult<IReadOnlyList<AssistantItem>>([new(t, "when", $"https://umat.test/{t.Replace(' ', '-')}")]);
        public Task<IReadOnlyList<AssistantItem>> PublicCampaignsAsync(AssistantCommunity c, CancellationToken ct = default) => Some("Library fund");
        public Task<IReadOnlyList<AssistantItem>> PublicEventsAsync(AssistantCommunity c, CancellationToken ct = default) => Some("Homecoming");
        public Task<IReadOnlyList<AssistantItem>> PublicJobsAsync(AssistantCommunity c, CancellationToken ct = default) => Some("Engineer");
        public Task<IReadOnlyList<AssistantItem>> PublicUpdatesAsync(AssistantCommunity c, CancellationToken ct = default) => Some("News item");
        public Task<IReadOnlyList<AssistantItem>> MemberCampaignsAsync(AssistantCommunity c, CancellationToken ct = default) => throw new InvalidOperationException("A group must never use member data");
        public Task<IReadOnlyList<AssistantItem>> MemberEventsAsync(AssistantCommunity c, CancellationToken ct = default) => throw new InvalidOperationException("A group must never use member data");
        public Task<IReadOnlyList<AssistantItem>> MemberJobsAsync(AssistantCommunity c, CancellationToken ct = default) => throw new InvalidOperationException("A group must never use member data");
        public Task<IReadOnlyList<AssistantItem>> MemberUpdatesAsync(AssistantCommunity c, CancellationToken ct = default) => throw new InvalidOperationException("A group must never use member data");
        public Task<AssistantDues?> MemberDuesAsync(AssistantCommunity c, CancellationToken ct = default) => throw new InvalidOperationException("A group must never use member data");
        public Task<string?> MemberInviteLinkAsync(AssistantCommunity c, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<bool> IsAvailableAsync(AssistantCommunity c, string feature, CancellationToken ct = default) => Task.FromResult(!Disabled.Contains(feature));
    }

    private sealed class Rig
    {
        public required GroupCommandHandler Handler { get; init; }
        public required string Db { get; init; }
        public required Content Content { get; init; }
        public required Mock<IWhatsAppProvider> Provider { get; init; }
    }

    private static Rig Create(params Institution[] institutions)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name)) { foreach (var i in institutions) db.Add(i); db.SaveChanges(); }
        var ctx = TestDb.Create(name);

        var provider = new Mock<IWhatsAppProvider>();
        provider.Setup(p => p.GetGroupAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new WhatsAppGroupDetails(Group, "UMaT Class of 2015"));
        var directory = new Mock<IAssistantDirectory>();
        directory.Setup(d => d.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => new AssistantCommunity(id, "umat", "UMaT Alumni", "https://umat.test"));
        var content = new Content();

        var handler = new GroupCommandHandler(new AlumniPgRepository<Institution>(ctx), directory.Object, content, provider.Object,
            new InMemoryRedisService<MemberRedisConfig>(),
            Options.Create(new WaSenderConfig { AssistantNumber = "233200000000" }));
        return new Rig { Handler = handler, Db = name, Content = content, Provider = provider };
    }

    private static WhatsAppInboundEvent Post(string text, string group = Group, string sender = "233244000111", bool fromMe = false)
        => new(WhatsAppInboundKind.GroupMessage, Guid.NewGuid().ToString("N"), sender, null, group, text, DateTimeOffset.UtcNow, fromMe);

    private static Institution Reload(string db, string id)
    {
        using var ctx = TestDb.Create(db);
        return Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreQueryFilters(ctx.Institutions).Single(i => i.Id == id);
    }

    // ── What is never read ───────────────────────────────────────────────

    [Theory]
    [InlineData("good morning everyone")]
    [InlineData("events? anyone going")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!")]
    public async Task Ordinary_conversation_is_never_answered(string text)
    {
        var rig = Create(Inst("i1", Active()));
        Assert.Null(await rig.Handler.HandleAsync(Post(text)));
    }

    [Fact]
    public async Task A_group_nobody_connected_gets_silence_even_for_a_valid_command()
    {
        var rig = Create(Inst("i1", Active()));
        Assert.Null(await rig.Handler.HandleAsync(Post("!events", group: "999@g.us")));
    }

    [Fact]
    public async Task The_assistants_own_messages_and_private_chats_are_ignored_here()
    {
        var rig = Create(Inst("i1", Active()));
        Assert.Null(await rig.Handler.HandleAsync(Post("!events", fromMe: true)));
        Assert.Null(await rig.Handler.HandleAsync(Post("!events") with { Kind = WhatsAppInboundKind.PersonalMessage }));
    }

    // ── Connecting ───────────────────────────────────────────────────────

    [Fact]
    public async Task The_code_proposes_the_group_and_does_not_activate_it()
    {
        var rig = Create(Inst("i1", Waiting()));

        var reply = await rig.Handler.HandleAsync(Post("!link k7m2qx9p", group: Group));

        Assert.Contains("needs to confirm", reply!.Text);
        var link = Assert.Single(Reload(rig.Db, "i1").WhatsAppGroups);
        Assert.Equal((WhatsAppGroupStatuses.Detected, Group, "UMaT Class of 2015", null), (link.Status, link.GroupJid, link.Name, link.Code));
        // Still unconfirmed, so it answers nothing.
        Assert.Null(await rig.Handler.HandleAsync(Post("!events")));
    }

    [Fact]
    public async Task A_code_works_once_and_a_wrong_or_expired_one_is_told_so_while_chatter_stays_silent()
    {
        var rig = Create(Inst("i1", Waiting()), Inst("i2", Waiting("EXPIRED22", DateTime.UtcNow.AddMinutes(-1))));

        var wrong = await rig.Handler.HandleAsync(Post("!link NOPE1234"));
        Assert.Contains("couldn't match that code", wrong!.Text);
        Assert.Contains("EXPIRED22".Length > 0 ? "24 hours" : "", (await rig.Handler.HandleAsync(Post("!link EXPIRED22")))!.Text);
        Assert.Null(await rig.Handler.HandleAsync(Post("!link hi there")));   // not shaped like a code: no answer
        Assert.Null(await rig.Handler.HandleAsync(Post("!link")));
        Assert.NotNull(await rig.Handler.HandleAsync(Post("!link K7M2QX9P")));
        // Already spent: another group posting it is told it didn't match, and nothing is connected.
        Assert.Contains("couldn't match that code", (await rig.Handler.HandleAsync(Post("!link K7M2QX9P", group: "777@g.us")))!.Text);
    }

    [Fact]
    public async Task A_group_already_connected_cannot_be_connected_again_with_another_code()
    {
        var rig = Create(Inst("i1", Active()), Inst("i2", Waiting()));

        var reply = await rig.Handler.HandleAsync(Post("!link K7M2QX9P"));

        Assert.Contains("already connected", reply!.Text);
        Assert.Equal(WhatsAppGroupStatuses.AwaitingGroup, Reload(rig.Db, "i2").WhatsAppGroups.Single().Status);
    }

    // ── Answering ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_connected_group_goes_quiet_when_the_platform_deactivates_its_institution()
    {
        var inst = Inst("i1", Active());
        inst.WhatsAppBotEnabled = false;
        var rig = Create(inst);

        Assert.Null(await rig.Handler.HandleAsync(Post("!events")));
        Assert.Null(await rig.Handler.HandleAsync(Post("!help")));
    }

    [Fact]
    public async Task A_connected_group_gets_public_information_with_exact_links_and_a_pointer_to_private_chat()
    {
        var rig = Create(Inst("i1", Active()));

        var events = await rig.Handler.HandleAsync(Post("!events"));

        Assert.Equal(Group, events!.To);
        Assert.Contains("Homecoming", events.Text);
        Assert.Contains("https://umat.test/Homecoming", events.Text);
        Assert.Contains("message me privately:\nhttps://wa.me/233200000000?text=start%20umat", events.Text);
    }

    [Theory]
    [InlineData("!help", "!events")]
    [InlineData("!campaigns", "Library fund")]
    [InlineData("!jobs", "Engineer")]
    [InlineData("!updates", "News item")]
    [InlineData("!community", "https://umat.test/register")]
    public async Task Each_command_answers_from_public_data_only(string command, string expected)
    {
        var rig = Create(Inst("i1", Active()));
        var reply = await rig.Handler.HandleAsync(Post(command));
        Assert.Contains(expected, reply!.Text);   // and the Content fake throws if any member method is touched
    }

    [Fact]
    public async Task Only_the_commands_an_administrator_allowed_are_answered()
    {
        var rig = Create(Inst("i1", Active("events", "help")));

        Assert.NotNull(await rig.Handler.HandleAsync(Post("!events")));
        Assert.Null(await rig.Handler.HandleAsync(Post("!jobs")));
        var help = await rig.Handler.HandleAsync(Post("!help"));
        Assert.Contains("!events", help!.Text);
        Assert.DoesNotContain("!jobs", help.Text);
    }

    [Fact]
    public async Task A_group_with_the_assistant_off_or_an_institution_that_disabled_whatsapp_is_silent()
    {
        var off = Active(); off.AssistantEnabled = false;
        var rig1 = Create(Inst("i1", off));
        Assert.Null(await rig1.Handler.HandleAsync(Post("!events")));

        var institution = Inst("i1", Active()); institution.DisabledFeatures = [InstitutionFeatures.WhatsApp];
        var rig2 = Create(institution);
        Assert.Null(await rig2.Handler.HandleAsync(Post("!events")));
    }

    [Fact]
    public async Task A_disabled_part_of_the_product_shows_nothing_for_it()
    {
        var rig = Create(Inst("i1", Active()));
        rig.Content.Disabled.Add(InstitutionFeatures.Events);

        var reply = await rig.Handler.HandleAsync(Post("!events"));
        Assert.Contains("No upcoming events", reply!.Text);
        Assert.DoesNotContain("Homecoming", reply.Text);
    }

    [Fact]
    public async Task An_unknown_command_gets_a_short_hint_and_a_flood_gets_one_notice_then_silence()
    {
        var rig = Create(Inst("i1", Active()));
        Assert.Contains("!help", (await rig.Handler.HandleAsync(Post("!dance")))!.Text);

        string?[] replies = new string?[14];
        for (var i = 0; i < replies.Length; i++) replies[i] = (await rig.Handler.HandleAsync(Post("!help")))?.Text;

        Assert.Contains(replies, r => r is not null && r.Contains("a lot of commands"));
        Assert.Null(replies[^1]);
    }
}
