namespace TaskEngine.Infrastructure.Monitoring;

/// <summary>
/// Debounces raw, per-keystroke-frequency file-change notifications into coarser activity
/// intervals - one per file path, spanning from its first observed change until it has been quiet
/// for at least the configured debounce window. Deliberately has no dependency on
/// <see cref="System.IO.FileSystemWatcher"/> or real time (<see cref="Record"/>/<see cref="Flush"/>
/// take an explicit timestamp), so it can be unit tested without real I/O or timers - the actual
/// wall-clock polling lives in <see cref="FileSystemActivityWatcher"/>.
/// </summary>
public sealed class FileActivityAggregator
{
    private readonly TimeSpan _debounceWindow;
    private readonly Dictionary<string, PendingChange> _pending = [];

    public FileActivityAggregator(TimeSpan debounceWindow)
    {
        if (debounceWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounceWindow), "Debounce window must be positive.");
        }

        _debounceWindow = debounceWindow;
    }

    /// <summary>
    /// Records a raw change to <paramref name="path"/> observed at <paramref name="timestamp"/>,
    /// extending its pending interval (or opening a new one).
    /// </summary>
    public void Record(string path, DateTimeOffset timestamp)
    {
        if (_pending.TryGetValue(path, out var pending))
        {
            if (timestamp > pending.LastSeenAt)
            {
                _pending[path] = pending with { LastSeenAt = timestamp };
            }
        }
        else
        {
            _pending[path] = new PendingChange(timestamp, timestamp);
        }
    }

    /// <summary>
    /// Closes and returns every pending interval that has been quiet for at least the debounce
    /// window as of <paramref name="now"/>, removing them from the pending set. Intervals still
    /// receiving changes are left pending for a future <see cref="Flush"/>.
    /// </summary>
    public IReadOnlyList<CompletedFileActivity> Flush(DateTimeOffset now)
    {
        List<string>? toRemove = null;
        var completed = new List<CompletedFileActivity>();

        foreach (var (path, pending) in _pending)
        {
            if (now - pending.LastSeenAt < _debounceWindow)
            {
                continue;
            }

            // ActivityInterval requires EndedAt strictly after StartedAt; a file touched only
            // once has StartedAt == LastSeenAt, so nudge the end by a tick to keep it valid.
            var endedAt = pending.LastSeenAt > pending.StartedAt
                ? pending.LastSeenAt
                : pending.StartedAt.AddTicks(1);

            completed.Add(new CompletedFileActivity(path, pending.StartedAt, endedAt));
            (toRemove ??= []).Add(path);
        }

        if (toRemove is not null)
        {
            foreach (var path in toRemove)
            {
                _pending.Remove(path);
            }
        }

        return completed;
    }

    private readonly record struct PendingChange(DateTimeOffset StartedAt, DateTimeOffset LastSeenAt);
}

public readonly record struct CompletedFileActivity(string Path, DateTimeOffset StartedAt, DateTimeOffset EndedAt);
