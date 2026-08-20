namespace TaskEngine.Infrastructure.Monitoring;

/// <summary>
/// Infrastructure-internal seam over the Win32 foreground-window APIs, kept out of
/// <c>TaskEngine.Application.Abstractions</c> for the same reason as <see cref="IRunningProcessesProvider"/>:
/// it is only needed by <see cref="WindowFocusActivityWatcher"/>, not by any application use case.
/// Exists so the watcher's polling/aggregation glue can be unit tested without real Win32 P/Invoke
/// calls or an actual foreground window.
/// </summary>
public interface IForegroundWindowSampler
{
    ForegroundWindowSample Sample();
}

/// <summary>
/// Snapshot of the current foreground window. Both fields are <c>null</c> when there is no
/// foreground window or its owning process/title could not be resolved (e.g. it exited mid-sample).
/// </summary>
public readonly record struct ForegroundWindowSample(string? ProcessName, string? WindowTitle);
