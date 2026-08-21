using System.Threading;
using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;
using TaskEngine.Domain.Monitoring;

namespace TaskEngine.Infrastructure.Monitoring;

/// <summary>
/// <see cref="IFileActivityWatcher"/> backed by <see cref="FileSystemWatcher"/> (RF-001,
/// ERS-Monitoramento.md; issue #12). Watches <see cref="FileActivityWatcherOptions.WatchedDirectories"/>
/// recursively, debounces the resulting flood of change notifications through
/// <see cref="FileActivityAggregator"/> (so one edit doesn't produce one interval per keystroke/
/// autosave), and persists a completed <see cref="ActivityInterval"/> per file once it has been
/// quiet for the debounce window. Origin classification (RF-003) delegates to
/// <see cref="ActivitySourceClassifier.ClassifyFromRunningProcesses"/>: issue #12 is specifically
/// about catching AI-agent edits made in the background (Claude Code, Copilot, scripts) while the
/// user isn't focused on the window, so a file change is attributed to
/// <see cref="ActivitySource.Ai"/> when a known AI agent process is running at flush time, and to
/// <see cref="ActivitySource.Human"/> otherwise. Runs independent of any task being in progress
/// (RN-001) - <see cref="Start"/> is called once at application startup, not gated by task state.
/// </summary>
public sealed class FileSystemActivityWatcher : IFileActivityWatcher, IDisposable
{
    private readonly FileActivityWatcherOptions _options;
    private readonly IMonitoredActivityRepository _repository;
    private readonly IRunningProcessesProvider _processesProvider;
    private readonly FileActivityAggregator _aggregator;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Lock _lock = new();
    private Timer? _flushTimer;

    public FileSystemActivityWatcher(
        FileActivityWatcherOptions options,
        IMonitoredActivityRepository repository,
        IRunningProcessesProvider processesProvider)
    {
        _options = options;
        _repository = repository;
        _processesProvider = processesProvider;
        _aggregator = new FileActivityAggregator(options.DebounceWindow);
    }

    public void Start()
    {
        if (_watchers.Count > 0)
        {
            return;
        }

        foreach (var directory in _options.WatchedDirectories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            var watcher = new FileSystemWatcher(directory)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            };
            watcher.Changed += OnFileEvent;
            watcher.Created += OnFileEvent;
            watcher.Renamed += OnFileEvent;
            watcher.Error += OnWatcherError;
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }

        _flushTimer = new Timer(_ => Flush(), null, _options.DebounceWindow, _options.DebounceWindow);
    }

    public void Stop()
    {
        foreach (FileSystemWatcher watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();

        _flushTimer?.Dispose();
        _flushTimer = null;
    }

    public void Dispose() => Stop();

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (IsNoise(e.FullPath))
        {
            return;
        }

        lock (_lock)
        {
            _aggregator.Record(e.FullPath, DateTimeOffset.UtcNow);
        }
    }

    /// <summary>
    /// <see cref="FileSystemWatcher"/> has a limited internal buffer; watching a large tree (the
    /// whole user profile, since RF-001/CreateDefault stopped scoping to a single project folder)
    /// under bursts of activity can overflow it, which raises <see cref="FileSystemWatcher.Error"/>
    /// and silently stops delivering further change notifications. Re-enabling
    /// <see cref="FileSystemWatcher.EnableRaisingEvents"/> is the documented minimal recovery: it
    /// resets the watcher so it keeps observing instead of going quiet for the rest of the process
    /// lifetime. Some events raised during the overflow window are unavoidably lost, but that is
    /// preferable to losing all future events.
    /// </summary>
    private static void OnWatcherError(object sender, ErrorEventArgs e)
    {
        if (sender is FileSystemWatcher watcher)
        {
            watcher.EnableRaisingEvents = false;
            watcher.EnableRaisingEvents = true;
        }
    }

    /// <summary>
    /// Filters out changes under directories that are noise for activity tracking purposes -
    /// build output, VCS internals, dependency caches, and OS/app temp and cache directories -
    /// which would otherwise dominate the captured history with churn unrelated to actual work.
    /// Watching the entire user profile (RF-001) is far noisier than a single project directory,
    /// so this list is deliberately broader than plain build-tool output. This is a heuristic, not
    /// an exhaustive deny-list: it only excludes directories that are reliably recognizable as
    /// noise by path convention. Anything not matched here is still captured - CA-001.2 only
    /// requires that recognized noise be excluded, not that every possible noise source is caught.
    /// </summary>
    public static bool IsNoise(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/.git/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase)
            // OS/system temp and per-app package data - UWP/Store apps under Packages are
            // extremely chatty and never represent user work.
            || normalized.Contains("/appdata/local/temp/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/appdata/local/packages/", StringComparison.OrdinalIgnoreCase)
            // Windows/legacy IE/Edge network cache.
            || normalized.Contains("/appdata/local/microsoft/windows/inetcache/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/appdata/local/microsoft/windows/webcache/", StringComparison.OrdinalIgnoreCase)
            // Dev tool caches: Visual Studio's cache/scratch folder, NuGet's package cache, and
            // the recycle bin (deletions routed there are not meaningful file activity).
            || normalized.Contains("/.vs/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/.nuget/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/$recycle.bin/", StringComparison.OrdinalIgnoreCase)
            // Generic app cache folders (AppData\Roaming|Local\...\Cache) plus the specific cache
            // directory names used by Chromium- and Gecko-based browsers (Chrome, Edge, Firefox)
            // under their profile folders (e.g. Google\Chrome\User Data\Default\Cache,
            // Mozilla\Firefox\Profiles\<profile>\cache2). Matched by segment name rather than a
            // hardcoded browser path so it survives arbitrary profile names.
            || normalized.Contains("/cache/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/cache2/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/code cache/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/gpucache/", StringComparison.OrdinalIgnoreCase);
    }

    private void Flush()
    {
        IReadOnlyList<CompletedFileActivity> completed;
        lock (_lock)
        {
            completed = _aggregator.Flush(DateTimeOffset.UtcNow);
        }

        if (completed.Count == 0)
        {
            return;
        }

        var source = ActivitySourceClassifier.ClassifyFromRunningProcesses(_processesProvider.GetRunningProcessNames());

        foreach (CompletedFileActivity change in completed)
        {
            var activity = new ActivityInterval(source, change.StartedAt, change.EndedAt, ActivityItemType.File, change.Path);
            PersistAsync(activity);
        }
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
}
