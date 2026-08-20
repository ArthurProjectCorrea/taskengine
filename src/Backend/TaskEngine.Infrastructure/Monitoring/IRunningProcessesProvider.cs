namespace TaskEngine.Infrastructure.Monitoring;

/// <summary>
/// Infrastructure-internal seam over process enumeration (<see cref="System.Diagnostics.Process.GetProcesses()"/>),
/// kept out of <c>TaskEngine.Application.Abstractions</c> because it is only ever needed by
/// watcher implementations here, not by any application use case. Exists purely so
/// <see cref="FileSystemActivityWatcher"/>'s origin classification can be unit tested without
/// depending on whatever processes happen to be running on the test machine.
/// </summary>
public interface IRunningProcessesProvider
{
    IReadOnlyList<string> GetRunningProcessNames();
}
