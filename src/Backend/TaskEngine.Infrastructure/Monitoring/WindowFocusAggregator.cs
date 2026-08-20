namespace TaskEngine.Infrastructure.Monitoring;

/// <summary>
/// Turns a stream of foreground-window polling samples into completed focus intervals: while the
/// same window (identified by owning process + title) stays in the foreground, it is one ongoing
/// interval; the moment focus moves to a different window, the previous interval is closed and
/// returned. Deliberately takes an explicit timestamp per observation instead of reading the clock
/// itself, so it can be unit tested without real polling/timers - the actual Win32 polling lives in
/// <see cref="WindowFocusActivityWatcher"/>.
/// </summary>
public sealed class WindowFocusAggregator
{
    private CurrentWindow? _current;

    /// <summary>
    /// Reports the foreground window observed at <paramref name="timestamp"/>. Returns the just-
    /// completed interval for the previously focused window when focus changed, or <c>null</c>
    /// when the same window is still focused (or there was no previous window).
    /// </summary>
    public CompletedFocus? Observe(string? processName, string? windowTitle, DateTimeOffset timestamp)
    {
        var isSameWindow = _current is { } current
            && string.Equals(current.ProcessName, processName, StringComparison.Ordinal)
            && string.Equals(current.WindowTitle, windowTitle, StringComparison.Ordinal);

        if (isSameWindow)
        {
            _current = _current!.Value with { LastSeenAt = timestamp };
            return null;
        }

        CompletedFocus? completed = null;
        if (_current is { } previous && previous.LastSeenAt > previous.StartedAt)
        {
            completed = new CompletedFocus(previous.ProcessName, previous.WindowTitle, previous.StartedAt, previous.LastSeenAt);
        }

        _current = string.IsNullOrEmpty(processName)
            ? null
            : new CurrentWindow(processName, windowTitle ?? string.Empty, timestamp, timestamp);

        return completed;
    }

    /// <summary>
    /// Closes and returns the currently focused window's interval as of <paramref name="now"/>,
    /// e.g. when the watcher is stopping and any in-progress focus should not be silently dropped.
    /// </summary>
    public CompletedFocus? Flush(DateTimeOffset now)
    {
        if (_current is not { } current || current.LastSeenAt <= current.StartedAt)
        {
            _current = null;
            return null;
        }

        _current = null;
        return new CompletedFocus(current.ProcessName, current.WindowTitle, current.StartedAt, now > current.LastSeenAt ? now : current.LastSeenAt);
    }

    private readonly record struct CurrentWindow(string ProcessName, string WindowTitle, DateTimeOffset StartedAt, DateTimeOffset LastSeenAt);
}

public readonly record struct CompletedFocus(string ProcessName, string WindowTitle, DateTimeOffset StartedAt, DateTimeOffset EndedAt);
