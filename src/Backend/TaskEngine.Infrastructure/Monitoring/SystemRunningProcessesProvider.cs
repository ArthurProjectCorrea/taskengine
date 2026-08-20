using System.Diagnostics;

namespace TaskEngine.Infrastructure.Monitoring;

/// <summary>
/// Real <see cref="IRunningProcessesProvider"/> backed by <see cref="Process.GetProcesses()"/>.
/// </summary>
public sealed class SystemRunningProcessesProvider : IRunningProcessesProvider
{
    public IReadOnlyList<string> GetRunningProcessNames()
    {
        Process[] processes = Process.GetProcesses();
        try
        {
            var names = new List<string>(processes.Length);
            foreach (Process process in processes)
            {
                try
                {
                    names.Add(process.ProcessName);
                }
                catch
                {
                    // Process exited between GetProcesses() and reading its name - skip it.
                }
            }

            return names;
        }
        finally
        {
            foreach (Process process in processes)
            {
                process.Dispose();
            }
        }
    }
}
