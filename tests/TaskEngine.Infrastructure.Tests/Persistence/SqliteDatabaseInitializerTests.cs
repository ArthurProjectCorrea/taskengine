using Microsoft.Data.Sqlite;
using TaskEngine.Infrastructure.Persistence;

namespace TaskEngine.Infrastructure.Tests.Persistence;

public class SqliteDatabaseInitializerTests
{
    private static readonly string[] ExpectedTables =
        ["activity_intervals", "app_settings", "tasks", "unmapped_time_entries", "work_sessions", "worklogs"];

    [Fact]
    public async Task EnsureCreatedAsync_FromScratch_CreatesFileAndAllTables()
    {
        using var db = new TempSqliteDatabase();
        Assert.False(File.Exists(db.PathProvider.DatabasePath));

        await db.InitializeAsync();

        Assert.True(File.Exists(db.PathProvider.DatabasePath));
        Assert.Equal(ExpectedTables, await GetTableNamesAsync(db.PathProvider));
    }

    [Fact]
    public async Task EnsureCreatedAsync_RunTwice_DoesNotThrowAndKeepsSchema()
    {
        using var db = new TempSqliteDatabase();

        await db.InitializeAsync();
        await db.InitializeAsync();

        Assert.Equal(ExpectedTables, await GetTableNamesAsync(db.PathProvider));
    }

    [Fact]
    public async Task EnsureCreatedAsync_RunTwice_KeepsTasksProviderIdColumnAndDoesNotThrow()
    {
        using var db = new TempSqliteDatabase();

        await db.InitializeAsync();
        await db.InitializeAsync();

        Assert.Contains("provider_id", await GetColumnNamesAsync(db.PathProvider, "tasks"));
    }

    [Fact]
    public async Task EnsureCreatedAsync_RunTwice_KeepsTasksProviderStatusNameColumnAndDoesNotThrow()
    {
        using var db = new TempSqliteDatabase();

        await db.InitializeAsync();
        await db.InitializeAsync();

        Assert.Contains("provider_status_name", await GetColumnNamesAsync(db.PathProvider, "tasks"));
    }

    [Fact]
    public async Task EnsureCreatedAsync_RunTwice_KeepsWorkSessionsTypeAndOriginColumnsAndDoesNotThrow()
    {
        using var db = new TempSqliteDatabase();

        await db.InitializeAsync();
        await db.InitializeAsync();

        var columns = await GetColumnNamesAsync(db.PathProvider, "work_sessions");
        Assert.Contains("type", columns);
        Assert.Contains("origin", columns);
    }

    private static async Task<List<string>> GetColumnNamesAsync(SqlitePathProvider pathProvider, string table)
    {
        await using var connection = new SqliteConnection(pathProvider.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info('{table}');";

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        var nameOrdinal = reader.GetOrdinal("name");
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(nameOrdinal));
        }

        return names;
    }

    private static async Task<List<string>> GetTableNamesAsync(SqlitePathProvider pathProvider)
    {
        await using var connection = new SqliteConnection(pathProvider.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name FROM sqlite_master
            WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
            """;

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
