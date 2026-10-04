using Temporalio.Client;
using Temporalio.Testing;

namespace ReservEase.Alumni.Operations.Worker.Tests;

/// <summary>
/// Starts one local Temporal dev server for the whole test run using the `temporal` CLI already on the machine
/// (set TEMPORAL_CLI to point elsewhere). Workflow tests are skipped, not failed, where the CLI is unavailable.
/// </summary>
public sealed class TemporalFixture : IAsyncLifetime
{
    public static string? CliPath { get; } = FindCli();
    public WorkflowEnvironment? Environment { get; private set; }
    public ITemporalClient Client => Environment!.Client;

    public async Task InitializeAsync()
    {
        if (CliPath is null) return;
        Environment = await WorkflowEnvironment.StartLocalAsync(new WorkflowEnvironmentStartLocalOptions
        {
            DevServerOptions = new() { ExistingPath = CliPath },
        });
    }

    public async Task DisposeAsync()
    {
        if (Environment is not null) await Environment.ShutdownAsync();
    }

    private static string? FindCli()
    {
        var explicitPath = System.Environment.GetEnvironmentVariable("TEMPORAL_CLI");
        if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath)) return explicitPath;
        var paths = (System.Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        return paths.Select(p => Path.Combine(p, "temporal")).FirstOrDefault(File.Exists);
    }
}

[CollectionDefinition("Temporal")]
public class TemporalCollection : ICollectionFixture<TemporalFixture> { }

/// <summary>A [Fact] that is skipped when no Temporal dev server binary is available.</summary>
public sealed class WorkflowFactAttribute : FactAttribute
{
    public WorkflowFactAttribute()
    {
        if (TemporalFixture.CliPath is null) Skip = "The `temporal` CLI is not installed (set TEMPORAL_CLI).";
    }
}

/// <summary>A [Theory] that is skipped when no Temporal dev server binary is available.</summary>
public sealed class WorkflowTheoryAttribute : TheoryAttribute
{
    public WorkflowTheoryAttribute()
    {
        if (TemporalFixture.CliPath is null) Skip = "The `temporal` CLI is not installed (set TEMPORAL_CLI).";
    }
}
