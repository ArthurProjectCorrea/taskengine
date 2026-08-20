using TaskEngine.Infrastructure.Monitoring;

namespace TaskEngine.Infrastructure.Tests.Monitoring;

internal sealed class FakeForegroundWindowSampler : IForegroundWindowSampler
{
    private readonly Lock _lock = new();
    private ForegroundWindowSample _current;

    public void SetCurrent(string? processName, string? windowTitle)
    {
        lock (_lock)
        {
            _current = new ForegroundWindowSample(processName, windowTitle);
        }
    }

    public ForegroundWindowSample Sample()
    {
        lock (_lock)
        {
            return _current;
        }
    }
}
