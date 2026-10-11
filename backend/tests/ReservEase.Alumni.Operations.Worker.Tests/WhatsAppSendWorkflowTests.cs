using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Notifications.Sdk.Workflows;
using ReservEase.Alumni.Operations.Worker.Workflows.Notifications;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.Whatsapp.Sdk.Models;
using ReservEase.Alumni.Whatsapp.Sdk.Services;
using Temporalio.Exceptions;
using Temporalio.Worker;

namespace ReservEase.Alumni.Operations.Worker.Tests;

[Collection("Temporal")]
public class WhatsAppSendWorkflowTests(TemporalFixture temporal)
{
    private static IConfiguration Config(bool enabled = true) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["WaSenderConfig:Enabled"] = enabled ? "true" : "false",
        ["WhatsAppAssistant:PaceMilliseconds"] = "0",
    }).Build();

    private static Mock<IWhatsAppProvider> Provider(WhatsAppSendResult? result = null)
    {
        var p = new Mock<IWhatsAppProvider>();
        p.Setup(x => x.SendMessageAsync(It.IsAny<WhatsAppOutboundMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(result ?? WhatsAppSendResult.Sent("m1"));
        return p;
    }

    private static WhatsAppSendActivities Activities(Mock<IWhatsAppProvider> p, bool enabled = true)
        => new(p.Object, Config(enabled), NullLogger<WhatsAppSendActivities>.Instance);

    // ── Activity behaviour (no Temporal server needed) ───────────────────

    [Fact]
    public async Task A_disabled_channel_sends_nothing()
    {
        var p = Provider();
        Assert.False(await Activities(p, enabled: false).SendAsync("233244123456", "hi"));
        p.Verify(x => x.SendMessageAsync(It.IsAny<WhatsAppOutboundMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_message_that_can_never_be_delivered_ends_quietly_instead_of_retrying()
    {
        var p = Provider(WhatsAppSendResult.Failed("HTTP 400", retryable: false));
        Assert.False(await Activities(p).SendAsync("233244123456", "hi"));
    }

    [Fact]
    public async Task A_provider_that_may_recover_is_thrown_so_temporal_retries_it()
    {
        var p = Provider(WhatsAppSendResult.Failed("HTTP 503", retryable: true));
        await Assert.ThrowsAsync<ApplicationFailureException>(() => Activities(p).SendAsync("233244123456", "hi"));
    }

    // ── Through Temporal ─────────────────────────────────────────────────

    [WorkflowFact]
    public async Task The_workflow_hands_the_message_to_the_provider_and_a_repeat_with_the_same_key_is_ignored()
    {
        var p = Provider();
        var queue = "q-" + Guid.NewGuid().ToString("N");
        var options = new TemporalWorkerOptions(queue).AddWorkflow<WhatsAppSendWorkflow>();
        options.AddAllActivities(typeof(WhatsAppSendActivities), Activities(p));
        using var worker = new TemporalWorker(temporal.Client, options);

        await worker.ExecuteAsync(async () =>
        {
            var key = "msg-" + Guid.NewGuid().ToString("N");
            var request = new WhatsAppSendRequest("233244123456", "Hello");
            var h1 = await temporal.Client.StartWorkflowAsync((IWhatsAppSendWorkflow w) => w.RunAsync(request),
                new Temporalio.Client.WorkflowOptions($"wa-out-{key}", queue) { IdReusePolicy = Temporalio.Api.Enums.V1.WorkflowIdReusePolicy.RejectDuplicate });
            await h1.GetResultAsync();

            await Assert.ThrowsAsync<Temporalio.Exceptions.WorkflowAlreadyStartedException>(() =>
                temporal.Client.StartWorkflowAsync((IWhatsAppSendWorkflow w) => w.RunAsync(request),
                    new Temporalio.Client.WorkflowOptions($"wa-out-{key}", queue) { IdReusePolicy = Temporalio.Api.Enums.V1.WorkflowIdReusePolicy.RejectDuplicate }));
        });

        p.Verify(x => x.SendMessageAsync(It.Is<WhatsAppOutboundMessage>(m => m.To == "233244123456" && m.Text == "Hello"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
