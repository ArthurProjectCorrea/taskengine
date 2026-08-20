using TaskEngine.Infrastructure.Monitoring;

namespace TaskEngine.Infrastructure.Tests.Monitoring;

internal sealed class FakeRunningProcessesProvider : IRunningProcessesProvider
{
    public IReadOnlyList<string> ProcessNames { get; set; } = [];

    public IReadOnlyList<string> GetRunningProcessNames() => ProcessNames;
}
