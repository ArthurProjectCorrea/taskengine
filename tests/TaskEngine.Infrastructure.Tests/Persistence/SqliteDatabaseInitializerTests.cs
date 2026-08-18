using Microsoft.Data.Sqlite;
using TaskEngine.Infrastructure.Persistence;

namespace TaskEngine.Infrastructure.Tests.Persistence;

public class SqliteDatabaseInitializerTests
{
    private static readonly string[] ExpectedTables =
        ["activity_intervals", "app_settings", "tasks", "work_sessions", "worklogs"];

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
