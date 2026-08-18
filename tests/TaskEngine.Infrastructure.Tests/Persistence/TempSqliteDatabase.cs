using Microsoft.Data.Sqlite;
using TaskEngine.Infrastructure.Persistence;

namespace TaskEngine.Infrastructure.Tests.Persistence;

/// <summary>
/// Points a <see cref="SqlitePathProvider"/> at a temporary on-disk file, deleting it (and
/// SQLite's -wal/-shm/-journal sidecar files) on dispose. Persistence tests exercise real SQLite
/// against this file rather than in-memory databases or mocks.
/// </summary>
internal sealed class TempSqliteDatabase : IDisposable
{
    public TempSqliteDatabase()
    {
        var path = Path.Combine(Path.GetTempPath(), $"taskengine-tests-{Guid.NewGuid()}.db");
        PathProvider = new SqlitePathProvider(path);
    }

    public SqlitePathProvider PathProvider { get; }

    public async Task InitializeAsync()
    {
        await new SqliteDatabaseInitializer(PathProvider).EnsureCreatedAsync();
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite pools native connections by connection string, keeping the file
        // handle open after a SqliteConnection is disposed. Clear the pool first so the temp
        // file can actually be deleted here.
        SqliteConnection.ClearPool(new SqliteConnection(PathProvider.ConnectionString));

        foreach (var suffix in new[] { string.Empty, "-wal", "-shm", "-journal" })
        {
            var file = PathProvider.DatabasePath + suffix;
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
