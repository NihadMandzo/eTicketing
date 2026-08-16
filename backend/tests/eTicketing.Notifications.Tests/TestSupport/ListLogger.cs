using Microsoft.Extensions.Logging;

namespace eTicketing.Notifications.Tests.TestSupport;

/// <summary>Captures every formatted log message written through it — used to assert a
/// sensitive value (raw reset token, Brevo API key) never appears in any log output, which a
/// Mock&lt;ILogger&gt; can't directly assert on since the formatted string is built internally
/// by the logging extension methods, not passed as a plain argument.</summary>
public sealed class ListLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));
    }
}
