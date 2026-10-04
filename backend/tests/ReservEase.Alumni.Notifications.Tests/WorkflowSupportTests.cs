using ReservEase.Alumni.Notifications.Sdk.Workflows;

namespace ReservEase.Alumni.Notifications.Tests;

public class WorkflowSupportTests
{
    [Fact]
    public void Workflow_id_is_prefixed_unique_and_has_no_dashes()
    {
        var ids = Enumerable.Range(0, 200).Select(_ => NotificationWorkflowId.New()).ToList();
        Assert.All(ids, id =>
        {
            Assert.StartsWith("notification-", id);
            Assert.DoesNotContain('-', id["notification-".Length..]);
            Assert.Equal("notification-".Length + 32, id.Length);
        });
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Dispatch_queue_name_is_the_one_the_worker_listens_on()
        => Assert.Equal("operations-notification-dispatch", NotificationTaskQueues.Dispatch);

    [Fact]
    public void Database_read_options_fail_fast_and_retry_forever_within_the_window()
    {
        var o = NotificationActivityOptions.DatabaseRead;
        Assert.Equal(TimeSpan.FromSeconds(15), o.StartToCloseTimeout);
        Assert.Equal(TimeSpan.FromMinutes(2), o.ScheduleToCloseTimeout);
        Assert.Equal(0, o.RetryPolicy!.MaximumAttempts);
    }

    [Fact]
    public void Database_write_options_allow_a_longer_retry_window_than_reads()
    {
        var read = NotificationActivityOptions.DatabaseRead;
        var write = NotificationActivityOptions.DatabaseWrite;
        Assert.True(write.ScheduleToCloseTimeout > read.ScheduleToCloseTimeout);
        Assert.True(write.RetryPolicy!.MaximumInterval > read.RetryPolicy!.MaximumInterval);
    }

    [Fact]
    public void External_gateway_options_have_a_per_attempt_timeout_below_the_overall_window()
    {
        var o = NotificationActivityOptions.ExternalGateway;
        Assert.True(o.StartToCloseTimeout < o.ScheduleToCloseTimeout);
        Assert.Equal(2.0f, o.RetryPolicy!.BackoffCoefficient);
        Assert.Equal(TimeSpan.FromSeconds(2), o.RetryPolicy.InitialInterval);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData("gateway")]
    public void Each_policy_has_a_backoff_that_never_shrinks_and_an_interval_cap(string which)
    {
        var o = which switch { "read" => NotificationActivityOptions.DatabaseRead, "write" => NotificationActivityOptions.DatabaseWrite, _ => NotificationActivityOptions.ExternalGateway };
        Assert.True(o.RetryPolicy!.BackoffCoefficient >= 1f);
        Assert.True(o.RetryPolicy.MaximumInterval >= o.RetryPolicy.InitialInterval);
    }
}
