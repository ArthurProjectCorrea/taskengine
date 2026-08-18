namespace TaskEngine.Infrastructure.Persistence;

/// <summary>
/// Resolves the SQLite database file path and guarantees its parent folder exists.
/// Defaults to <c>%LOCALAPPDATA%\TaskEngine\taskengine.db</c> (per-machine, no cloud sync,
/// consistent with the V1 roadmap), but accepts an explicit path override - used by tests to
/// point at a temporary file instead of the real user data folder.
/// </summary>
public sealed class SqlitePathProvider
{
    private const string AppFolderName = "TaskEngine";
    private const string DatabaseFileName = "taskengine.db";

    public SqlitePathProvider()
        : this(ResolveDefaultPath())
    {
    }

    public SqlitePathProvider(string databasePath)
    {
        DatabasePath = databasePath;

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public string DatabasePath { get; }

    public string ConnectionString => $"Data Source={DatabasePath}";

    private static string ResolveDefaultPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, AppFolderName, DatabaseFileName);
    }
}
