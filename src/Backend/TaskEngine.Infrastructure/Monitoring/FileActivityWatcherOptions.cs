namespace TaskEngine.Infrastructure.Monitoring;

/// <summary>
/// Configuration for <see cref="FileSystemActivityWatcher"/>. <see cref="WatchedDirectories"/> is
/// deliberately just a list of roots rather than a single "active project" concept: which
/// directory counts as the working project is left open by ERS-Monitoramento.md ("o escopo exato
/// ... está em aberto"), and there is no settings UI yet (out of scope for this change) to let the
/// user configure it. <see cref="CreateDefault"/> picks a reasonable placeholder
/// (<c>%USERPROFILE%\project</c>, matching this machine's actual dev layout) until a settings
/// screen exists to make it user-configurable.
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
        var defaultDirectory = Path.Combine(userProfile, "project");
        return new FileActivityWatcherOptions([defaultDirectory]);
    }
}
