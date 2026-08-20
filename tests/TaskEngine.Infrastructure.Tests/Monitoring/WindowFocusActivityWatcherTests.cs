using TaskEngine.Domain.Entities;
using TaskEngine.Infrastructure.Monitoring;

namespace TaskEngine.Infrastructure.Tests.Monitoring;

public class WindowFocusActivityWatcherTests
{
    [Theory]
    [InlineData("chrome", true)]
    [InlineData("msedge", true)]
    [InlineData("firefox", true)]
    [InlineData("MSEDGE", true)]
    [InlineData("devenv", false)]
    [InlineData("explorer", false)]
    [InlineData("Code", false)]
    public void IsKnownBrowser_RecognizesKnownBrowserProcessNamesCaseInsensitively(string processName, bool expected)
    {
        Assert.Equal(expected, WindowFocusActivityWatcher.IsKnownBrowser(processName));
    }

    [Fact]
    public async Task Start_WhenFocusMovesAwayFromABrowser_PersistsTheBrowserIntervalAsHumanActivity()
    {
        var options = new WindowFocusWatcherOptions(TimeSpan.FromMilliseconds(50));
        var repository = new FakeMonitoredActivityRepository();
        var sampler = new FakeForegroundWindowSampler();
        sampler.SetCurrent("chrome", "GitHub - taskengine");

        using var watcher = new WindowFocusActivityWatcher(options, repository, sampler);
        watcher.Start();

        // Let a few polls accumulate on the same window so it has a non-zero observed duration.
        await Task.Delay(300);

        // Switch focus away from the browser - this should close and persist the interval above.
        sampler.SetCurrent("devenv", "Solution Explorer");

        var activity = await WaitForActivityAsync(repository, TimeSpan.FromSeconds(10));

        Assert.Equal(ActivityItemType.Browser, activity.Type);
        Assert.Equal(ActivitySource.Human, activity.Source);
        Assert.Equal("GitHub - taskengine", activity.Path);
    }

    [Fact]
    public async Task Start_WhenFocusedWindowIsNotABrowser_NeverPersistsAnything()
    {
        var options = new WindowFocusWatcherOptions(TimeSpan.FromMilliseconds(50));
        var repository = new FakeMonitoredActivityRepository();
        var sampler = new FakeForegroundWindowSampler();
        sampler.SetCurrent("devenv", "Solution Explorer");

        using var watcher = new WindowFocusActivityWatcher(options, repository, sampler);
        watcher.Start();

        await Task.Delay(300);
        sampler.SetCurrent("explorer", "Documents");
        await Task.Delay(300);

        Assert.Empty(repository.Activities);
    }

    private static async Task<ActivityInterval> WaitForActivityAsync(FakeMonitoredActivityRepository repository, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (repository.Activities.Count > 0)
            {
                return repository.Activities[0];
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Watcher did not persist an activity within the expected time.");
    }
}
