namespace TaskEngine.Application.Abstractions;

/// <summary>
/// Port for continuous file-change monitoring (RF-001, ERS-Monitoramento.md). Implementations
/// observe known working directories and persist detected activity via
/// <see cref="IMonitoredActivityRepository"/> - independent of any task being in progress
/// (RN-001), so <see cref="Start"/> is meant to be called once at application startup, not gated
/// by task state.
/// </summary>
public interface IFileActivityWatcher
{
    void Start();

    void Stop();
}
