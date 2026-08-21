using System.Reflection;
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
    [InlineData(@"C:\Users\arthur\AppData\Local\Temp\tmp1234.tmp", true)]
    [InlineData(@"C:\Users\arthur\AppData\Local\Packages\Microsoft.WindowsTerminal\LocalState\settings.json", true)]
    [InlineData(@"C:\Users\arthur\AppData\Local\Google\Chrome\User Data\Default\Cache\data_1", true)]
    [InlineData(@"C:\Users\arthur\AppData\Local\Mozilla\Firefox\Profiles\abc.default\cache2\entries\1", true)]
    [InlineData(@"C:\Users\arthur\AppData\Local\Microsoft\Edge\User Data\Default\Code Cache\js\index", true)]
    [InlineData(@"C:\Users\arthur\.vs\TaskEngine\v17\.suo", true)]
    [InlineData(@"C:\Users\arthur\.nuget\packages\newtonsoft.json\13.0.1\pkg.nupkg", true)]
    [InlineData(@"C:\$RECYCLE.BIN\S-1-5-21\file.txt", true)]
    [InlineData(@"C:\Users\arthur\AppData\Local\Microsoft\Windows\INetCache\image.png", true)]
    [InlineData(@"C:\Users\arthur\Documents\report.docx", false)]
    public void IsNoise_FiltersUserProfileNoiseDirectories(string path, bool expectedNoise)
    {
        Assert.Equal(expectedNoise, FileSystemActivityWatcher.IsNoise(path));
    }

    [Fact]
    public async Task Start_OnFileChange_PersistsActivityAfterDebounceWindow()
    {
        var tempDirectory = CreateNonNoiseTestDirectory();
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
        var tempDirectory = CreateNonNoiseTestDirectory();
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

    [Fact]
    public void OnWatcherError_ReEnablesRaisingEvents_WithoutThrowing()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"taskengine-watcher-error-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            using var watcher = new FileSystemWatcher(tempDirectory) { EnableRaisingEvents = true };

            var handler = typeof(FileSystemActivityWatcher).GetMethod(
                "OnWatcherError",
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("OnWatcherError handler not found via reflection.");

            var errorArgs = new ErrorEventArgs(new IOException("simulated buffer overflow"));

            var exception = Record.Exception(() => handler.Invoke(null, [watcher, errorArgs]));

            Assert.Null(exception);
            Assert.True(watcher.EnableRaisingEvents);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Creates a scratch directory for watcher tests outside any path <see cref="FileSystemActivityWatcher.IsNoise"/>
    /// excludes. <see cref="Path.GetTempPath"/> resolves under <c>%LOCALAPPDATA%\Temp</c>, which is
    /// itself one of the noise patterns now that the watcher scopes to the whole user profile, so
    /// tests need a directory elsewhere under the profile to actually observe events.
    /// </summary>
    private static string CreateNonNoiseTestDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var directory = Path.Combine(userProfile, $"taskengine-watcher-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(directory);
        return directory;
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
