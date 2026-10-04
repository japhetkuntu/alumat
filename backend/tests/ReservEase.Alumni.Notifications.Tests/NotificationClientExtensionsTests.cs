using Microsoft.Extensions.Logging;
using Moq;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using Temporalio.Client;

namespace ReservEase.Alumni.Notifications.Tests;

public class NotificationClientExtensionsTests
{
    [Fact]
    public async Task Unavailable_temporal_drops_the_notification_with_a_warning()
    {
        var provider = new Mock<ITemporalClientProvider>();
        provider.SetupGet(p => p.IsAvailable).Returns(false);
        var logger = new CapturingLogger();

        await provider.Object.EnqueueNotificationAsync(NotificationRequest.JobAlert("inst-1", "job-1"), logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("JobAlert", entry.Message);
        Assert.Contains("inst-1", entry.Message);
        provider.VerifyGet(p => p.Client, Times.Never);
    }

    [Fact]
    public async Task A_failing_client_is_logged_and_never_thrown_to_the_caller()
    {
        var provider = new Mock<ITemporalClientProvider>();
        provider.SetupGet(p => p.IsAvailable).Returns(true);
        provider.SetupGet(p => p.Client).Returns(new Mock<ITemporalClient>(MockBehavior.Strict).Object); // strict mock: any workflow start throws
        var logger = new CapturingLogger();

        await provider.Object.EnqueueNotificationAsync(NotificationRequest.CampaignAlert("inst-2", "c1"), logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.NotNull(entry.Exception);
        Assert.Contains("CampaignAlert", entry.Message);
    }
}
