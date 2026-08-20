using System.Threading;
using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Infrastructure.Monitoring;

/// <summary>
/// <see cref="IWindowFocusWatcher"/> implementation: polls the foreground window on a timer
/// (RF-002, ERS-Monitoramento.md; issue #11) and, whenever a completed focus interval belongs to a
/// known browser process, persists it as an <see cref="ActivityInterval"/> of type
/// <see cref="ActivityItemType.Browser"/> - the window title stands in for the visited page/address
/// (architecture decision: title-based approximation instead of a browser extension, see task
/// context). Origin is always <see cref="ActivitySource.Human"/> here: per the ERS agent matrix,
/// RF-002 only lists AGT-001 (human) and AGT-003 (OS) as sources, unlike RF-001/RF-003 which also
/// list AGT-002 (AI agent) - browser focus is inherently a human interaction. Runs independent of
/// any task being in progress (RN-001) - <see cref="Start"/> is called once at application
/// startup, not gated by task state.
/// </summary>
public sealed class WindowFocusActivityWatcher : IWindowFocusWatcher, IDisposable
{
    private static readonly string[] KnownBrowserProcessNames =
    [
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "iexplore",
    ];

    private readonly WindowFocusWatcherOptions _options;
    private readonly IMonitoredActivityRepository _repository;
    private readonly IForegroundWindowSampler _sampler;
    private readonly WindowFocusAggregator _aggregator = new();
    private readonly Lock _lock = new();
    private Timer? _pollTimer;

    public WindowFocusActivityWatcher(
        WindowFocusWatcherOptions options,
        IMonitoredActivityRepository repository,
        IForegroundWindowSampler sampler)
    {
        _options = options;
        _repository = repository;
        _sampler = sampler;
    }

    public void Start()
    {
        if (_pollTimer is not null)
        {
            return;
        }

        _pollTimer = new Timer(_ => Poll(), null, TimeSpan.Zero, _options.PollingInterval);
    }

    public void Stop()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;

        CompletedFocus? completed;
        lock (_lock)
        {
            completed = _aggregator.Flush(DateTimeOffset.UtcNow);
        }

        HandleCompletedFocusIfBrowser(completed);
    }

    public void Dispose() => Stop();

    private void Poll()
    {
        ForegroundWindowSample sample = _sampler.Sample();

        CompletedFocus? completed;
        lock (_lock)
        {
            completed = _aggregator.Observe(sample.ProcessName, sample.WindowTitle, DateTimeOffset.UtcNow);
        }

        HandleCompletedFocusIfBrowser(completed);
    }

    private void HandleCompletedFocusIfBrowser(CompletedFocus? completed)
    {
        if (completed is not { } focus || !IsKnownBrowser(focus.ProcessName))
        {
            return;
        }

        var activity = new ActivityInterval(
            ActivitySource.Human, focus.StartedAt, focus.EndedAt, ActivityItemType.Browser, focus.WindowTitle);
        PersistAsync(activity);
    }

    private async void PersistAsync(ActivityInterval activity)
    {
        try
        {
            await _repository.AddAsync(activity, CancellationToken.None);
        }
        catch
        {
            // Best-effort background persistence: monitoring must never crash the app over a
            // transient I/O failure (e.g. the DB file briefly locked by a concurrent write).
        }
    }

    public static bool IsKnownBrowser(string processName)
    {
        foreach (var known in KnownBrowserProcessNames)
        {
            if (string.Equals(processName, known, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
