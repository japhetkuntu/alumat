using Temporalio.Workflows;

namespace ReservEase.Alumni.Reports.Sdk.Workflows;

/// <summary>
/// Generates one requested report from start to finish: marks it running, builds the file,
/// stores it privately, marks it ready and tells the requester. Implemented by Operations.Worker.
///
/// A report is a workflow rather than something an API request does inline because it can take
/// minutes over a large institution, and a web request that long times out at the proxy, ties up
/// an API thread and is lost if the instance restarts. Here the API only records the request and
/// returns; the worker takes whatever time it needs, survives a restart mid-report, and retries a
/// transient database or storage failure without the requester having to ask again.
///
/// One short-lived execution per report, keyed by the report job's id (see ReportWorkflowId), the
/// same request-scoped shape as every other workflow in this codebase.
/// </summary>
[Workflow("GenerateReport")]
public interface IGenerateReportWorkflow
{
    [WorkflowRun]
    Task RunAsync(string reportJobId);
}

public static class ReportWorkflowId
{
    public static string For(string reportJobId) => $"generate-report-{reportJobId}";
}

public static class ReportTaskQueues
{
    /// <summary>
    /// Its own queue, so a long report never sits in front of a payment callback or a notification,
    /// and so report concurrency can be capped on its own (see Operations.Worker's Program.cs).
    /// </summary>
    public const string Generation = "operations-report-generation";
}
