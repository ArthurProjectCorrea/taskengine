using Microsoft.Data.Sqlite;
using TaskEngine.Application.Abstractions;

namespace TaskEngine.Infrastructure.Persistence;

/// <summary>
/// <see cref="IAppSettingsStore"/> implementation backed by SQLite (<c>app_settings</c>
/// key/value table) - the same table issue #17 will reuse for encrypted credential storage.
/// </summary>
public sealed class SqliteAppSettingsStore : IAppSettingsStore
{
    private readonly SqlitePathProvider _pathProvider;

    public SqliteAppSettingsStore(SqlitePathProvider pathProvider)
    {
        _pathProvider = pathProvider;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM app_settings WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO app_settings (key, value)
            VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM app_settings WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_pathProvider.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
