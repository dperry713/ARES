namespace Ares.Infrastructure.Journal;

/// <summary>
/// Ambient actor attribution. The API sets this per request from the
/// authenticated principal so every journaled state transition and audit
/// record carries a real subject instead of "system". Services never trust a
/// caller-supplied actor string for authorization — this is attribution only.
/// </summary>
public static class JournalActor
{
    private static readonly AsyncLocal<string?> Current = new();

    public static string Name => Current.Value ?? "system";

    public static IDisposable Push(string actor)
    {
        var previous = Current.Value;
        Current.Value = string.IsNullOrWhiteSpace(actor) ? "system" : actor;
        return new Scope(previous);
    }

    private sealed class Scope(string? previous) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Current.Value = previous;
        }
    }
}
