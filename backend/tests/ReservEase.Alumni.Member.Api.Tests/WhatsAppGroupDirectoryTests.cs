using Microsoft.Extensions.Options;
using Moq;
using ReservEase.Alumni.Member.Api.Services.Assistant;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.TestKit;
using ReservEase.Alumni.Whatsapp.Sdk.Options;

namespace ReservEase.Alumni.Member.Api.Tests;

/// <summary>
/// A member is offered a community's WhatsApp group only when the platform has the assistant on. Every gate must hide it: the server not
/// set up, no assistant number, the platform switch off, the platform deactivating it for the institution, or the institution turning it off.
/// </summary>
public class WhatsAppGroupDirectoryTests
{
    private const string Tenant = "inst-1";

    private static async Task<IReadOnlyList<JoinableWhatsAppGroup>> Groups(
        bool serverEnabled = true, string number = "233200000000", bool platformSwitchOn = true, bool institutionBot = true, string[]? disabled = null)
    {
        var name = TestDb.NewName();
        using (var seed = TestDb.Create(name))
        {
            seed.Institutions.Add(new Institution
            {
                Id = Tenant, Slug = "umat", Name = "UMaT", WhatsAppBotEnabled = institutionBot, DisabledFeatures = [.. disabled ?? []],
                WhatsAppGroups = [new WhatsAppGroupLink { Name = "Class of 2015", Status = WhatsAppGroupStatuses.Active, InviteLink = "https://chat.whatsapp.com/abc" }],
            });
            seed.SaveChanges();
        }
        var db = TestDb.Create(name, Tenant);
        var sw = new Mock<IPlatformWhatsAppSwitch>(); sw.Setup(s => s.IsOnAsync()).ReturnsAsync(platformSwitchOn);
        var directory = new WhatsAppGroupDirectory(
            TestDb.Tenant(Tenant, "umat"), new AlumniPgRepository<Institution>(db), new AlumniPgRepository<CommunityMembership>(db), new AlumniPgRepository<Community>(db),
            Options.Create(new WaSenderConfig { Enabled = serverEnabled, AssistantNumber = number }), sw.Object);
        return await directory.ForMemberAsync("m1");
    }

    [Fact]
    public async Task With_every_gate_open_the_group_is_offered()
        => Assert.Equal("Class of 2015", Assert.Single(await Groups()).Name);

    [Fact]
    public async Task Nothing_is_offered_when_the_server_is_not_set_up_for_whatsapp()
        => Assert.Empty(await Groups(serverEnabled: false));

    [Fact]
    public async Task Nothing_is_offered_without_an_assistant_number()
        => Assert.Empty(await Groups(number: ""));

    [Fact]
    public async Task Nothing_is_offered_when_the_platform_switch_is_off()
        => Assert.Empty(await Groups(platformSwitchOn: false));

    [Fact]
    public async Task Nothing_is_offered_when_the_platform_deactivated_whatsapp_for_this_institution()
        => Assert.Empty(await Groups(institutionBot: false));

    [Fact]
    public async Task Nothing_is_offered_when_the_institution_turned_whatsapp_off()
        => Assert.Empty(await Groups(disabled: ["WhatsApp"]));
}
