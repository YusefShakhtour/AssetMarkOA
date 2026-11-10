using Microsoft.Extensions.Logging;

namespace AssetMarkOA.Tests;

// Collects log entries in memory so tests can check what would be written to the log file.
internal sealed class TestLogSink
{
    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    // Everything that would end up in the log file, including exception details.
    public string AllText => string.Join("\n", Entries.Select(e => $"{e.Message} {e.Exception}"));
}

internal sealed class TestLogger<T>(TestLogSink sink) : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= sink.MinimumLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            sink.Entries.Add((logLevel, formatter(state, exception), exception));
        }
    }
}
