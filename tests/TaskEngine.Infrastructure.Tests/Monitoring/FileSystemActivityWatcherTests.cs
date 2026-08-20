using TaskEngine.Domain.Entities;
using TaskEngine.Infrastructure.Monitoring;

namespace TaskEngine.Infrastructure.Tests.Monitoring;

public class FileSystemActivityWatcherTests
{
    [Theory]
    [InlineData(@"C:\repo\src\Foo.cs", false)]
    [InlineData(@"C:\repo\bin\Debug\Foo.dll", true)]
    [InlineData(@"C:\repo\obj\Debug\Foo.cs", true)]
    [InlineData(@"C:\repo\.git\index", true)]
    [InlineData(@"C:\repo\node_modules\pkg\index.js", true)]
    [InlineData("C:/repo/src/Foo.cs", false)]
    [InlineData("C:/repo/bin/Debug/Foo.dll", true)]
    public void IsNoise_FiltersBuildAndVcsDirectories(string path, bool expectedNoise)
    {
        Assert.Equal(expectedNoise, FileSystemActivityWatcher.IsNoise(path));
    }

    [Fact]
    public async Task Start_OnFileChange_PersistsActivityAfterDebounceWindow()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"taskengine-watcher-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var options = new FileActivityWatcherOptions([tempDirectory], TimeSpan.FromMilliseconds(200));
            var repository = new FakeMonitoredActivityRepository();
            var processesProvider = new FakeRunningProcessesProvider { ProcessNames = ["Claude"] };

            using var watcher = new FileSystemActivityWatcher(options, repository, processesProvider);
            watcher.Start();

            var filePath = Path.Combine(tempDirectory, "notes.txt");
            await File.WriteAllTextAsync(filePath, "first change");

            var activity = await WaitForActivityAsync(repository, TimeSpan.FromSeconds(10));

            Assert.Equal(ActivityItemType.File, activity.Type);
            Assert.Equal(ActivitySource.Ai, activity.Source);
            Assert.Equal(filePath, activity.Path);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Start_WithNoKnownAiProcessRunning_ClassifiesActivityAsHuman()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"taskengine-watcher-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var options = new FileActivityWatcherOptions([tempDirectory], TimeSpan.FromMilliseconds(200));
            var repository = new FakeMonitoredActivityRepository();
            var processesProvider = new FakeRunningProcessesProvider { ProcessNames = ["explorer", "notepad"] };

            using var watcher = new FileSystemActivityWatcher(options, repository, processesProvider);
            watcher.Start();

            await File.WriteAllTextAsync(Path.Combine(tempDirectory, "notes.txt"), "first change");

            var activity = await WaitForActivityAsync(repository, TimeSpan.FromSeconds(10));

            Assert.Equal(ActivitySource.Human, activity.Source);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
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
