namespace TaskEngine.Application.Abstractions;

/// <summary>
/// Port for continuous active-window-focus monitoring (RF-002, ERS-Monitoramento.md; issue #11).
/// Implementations poll the foreground window and persist a
/// <see cref="TaskEngine.Domain.Entities.ActivityInterval"/> of type
/// <see cref="TaskEngine.Domain.Entities.ActivityItemType.Browser"/> via
/// <see cref="IMonitoredActivityRepository"/> whenever a known browser process holds focus, using
/// the window title as an approximation of the visited page. Runs independent of any task being
/// in progress (RN-001), so <see cref="Start"/> is meant to be called once at application startup,
/// not gated by task state.
/// </summary>
public interface IWindowFocusWatcher
{
    void Start();

    void Stop();
}
