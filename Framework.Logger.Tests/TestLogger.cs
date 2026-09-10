using Microsoft.Extensions.Logging;

namespace Framework.Logger.Tests;

internal sealed class TestLogger<T> : ILogger<T>
{
    private readonly List<object> _activeScopes = [];

    public List<TestLogEntry> Entries { get; } = [];

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        _activeScopes.Add(state);
        return new Scope(() => _activeScopes.Remove(state));
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new TestLogEntry(logLevel, formatter(state, exception)));
    }

    public string? GetCurrentScopeValue(string key)
    {
        return _activeScopes
            .OfType<IEnumerable<KeyValuePair<string, object>>>()
            .SelectMany(scope => scope)
            .Where(property => property.Key == key)
            .Select(property => property.Value?.ToString())
            .LastOrDefault();
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action _dispose;
        private bool _isDisposed;

        public Scope(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _dispose();
            _isDisposed = true;
        }
    }
}

internal sealed record TestLogEntry(LogLevel Level, string Message);
