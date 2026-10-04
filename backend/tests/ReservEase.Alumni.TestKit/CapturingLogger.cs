using Microsoft.Extensions.Logging;

namespace ReservEase.Alumni.TestKit;

public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

/// <summary>An <see cref="ILogger"/> that keeps every entry so tests can assert on what was logged.</summary>
public sealed class CapturingLogger : ILogger
{
    public List<LogEntry> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
}

public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly CapturingLogger _inner = new();
    public List<LogEntry> Entries => _inner.Entries;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _inner.Log(logLevel, eventId, state, exception, formatter);
}
