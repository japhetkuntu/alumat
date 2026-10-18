using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Controllers;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>
/// The institution portal shows nothing about WhatsApp unless the platform has it on for this institution. The theme the portal loads
/// carries that answer, and it must be false in every case where any one of the platform's gates is closed.
/// </summary>
public class InstitutionThemeWhatsAppTests
{
    private static async Task<InstitutionThemeResponse> Theme(
        bool serverEnabled = true, string? number = "233200000000", bool platformSwitchOn = true, bool institutionBot = true, string[]? disabled = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WaSenderConfig:Enabled"] = serverEnabled ? "true" : "false",
            ["WaSenderConfig:AssistantNumber"] = number,
        }).Build();
        var sw = new Mock<IPlatformWhatsAppSwitch>();
        sw.Setup(s => s.IsOnAsync()).ReturnsAsync(platformSwitchOn);
        var controller = new PublicController(config, sw.Object) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        controller.HttpContext.Items["Institution"] = new InstitutionEntity
        {
            Slug = "umat", Name = "UMaT", WhatsAppBotEnabled = institutionBot, DisabledFeatures = [.. disabled ?? []],
        };
        var ok = Assert.IsType<OkObjectResult>(await controller.GetInstitutionTheme());
        return Assert.IsType<ApiResponse<InstitutionThemeResponse>>(ok.Value).Data!;
    }

    [Fact]
    public async Task It_is_available_when_every_platform_gate_is_open()
        => Assert.True((await Theme()).WhatsAppPlatformAvailable);

    [Fact]
    public async Task It_is_not_available_when_the_server_is_not_set_up_for_it()
        => Assert.False((await Theme(serverEnabled: false)).WhatsAppPlatformAvailable);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task It_is_not_available_without_an_assistant_number(string? number)
        => Assert.False((await Theme(number: number)).WhatsAppPlatformAvailable);

    [Fact]
    public async Task It_is_not_available_when_the_platform_switch_is_off_for_everyone()
        => Assert.False((await Theme(platformSwitchOn: false)).WhatsAppPlatformAvailable);

    [Fact]
    public async Task It_is_not_available_when_the_platform_deactivated_it_for_this_institution()
        => Assert.False((await Theme(institutionBot: false)).WhatsAppPlatformAvailable);

    [Fact]
    public async Task An_institution_turning_it_off_itself_does_not_change_what_the_platform_allows()
    {
        // The institution's own choice arrives in DisabledFeatures; the platform answer stays true so the settings tab can offer to turn it back on.
        var theme = await Theme(disabled: ["WhatsApp"]);
        Assert.True(theme.WhatsAppPlatformAvailable);
        Assert.Contains("WhatsApp", theme.DisabledFeatures);
    }
}
