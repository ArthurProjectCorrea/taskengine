namespace TaskEngine.Infrastructure.Monitoring;

/// <summary>
/// Configuration for <see cref="WindowFocusActivityWatcher"/>. Polling (rather than a Win32 hook)
/// is deliberately simple/coarse - RNF-001 asks for minimal footprint, and a 1-2s cadence is
/// imperceptible while still being precise enough for time-tracking purposes.
/// </summary>
public sealed class WindowFocusWatcherOptions
{
    public TimeSpan PollingInterval { get; }

    public WindowFocusWatcherOptions(TimeSpan? pollingInterval = null)
    {
        PollingInterval = pollingInterval ?? TimeSpan.FromSeconds(1.5);
    }
}
