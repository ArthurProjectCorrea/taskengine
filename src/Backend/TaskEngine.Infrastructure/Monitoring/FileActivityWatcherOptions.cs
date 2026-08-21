namespace TaskEngine.Infrastructure.Monitoring;

/// <summary>
/// Configuration for <see cref="FileSystemActivityWatcher"/>. <see cref="WatchedDirectories"/> is
/// deliberately just a list of roots rather than a single "active project" concept: RF-001 in
/// ERS-Monitoramento.md requires registering every file change on the computer regardless of any
/// task being in progress, with no notion of a scoped "project" directory. <see cref="CreateDefault"/>
/// therefore watches the entire logged-in user's profile (<c>%USERPROFILE%</c>) automatically, with
/// no manual directory configuration screen required. The list shape is kept (rather than a single
/// path) so a future settings UI can add or narrow roots without a breaking change to this type.
/// </summary>
public sealed class FileActivityWatcherOptions
{
    public IReadOnlyList<string> WatchedDirectories { get; }

    public TimeSpan DebounceWindow { get; }

    public FileActivityWatcherOptions(IReadOnlyList<string> watchedDirectories, TimeSpan? debounceWindow = null)
    {
        ArgumentNullException.ThrowIfNull(watchedDirectories);

        WatchedDirectories = watchedDirectories;
        DebounceWindow = debounceWindow ?? TimeSpan.FromSeconds(5);
    }

    public static FileActivityWatcherOptions CreateDefault()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new FileActivityWatcherOptions([userProfile]);
    }
}
