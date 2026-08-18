using Microsoft.Data.Sqlite;

namespace TaskEngine.Infrastructure.Persistence;

/// <summary>
/// Provisions the SQLite schema on first use. No versioned migrations for now - schema is
/// created via idempotent <c>CREATE TABLE IF NOT EXISTS</c> statements, re-run safely on every
/// app start (see issue #16). Versioned migrations become a real need only once the schema is
/// live in production with user data.
/// </summary>
public sealed class SqliteDatabaseInitializer
{
    private readonly SqlitePathProvider _pathProvider;

    public SqliteDatabaseInitializer(SqlitePathProvider pathProvider)
    {
        _pathProvider = pathProvider;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_pathProvider.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS tasks (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                description TEXT NULL,
                status TEXT NOT NULL,
                provider_task_id TEXT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS work_sessions (
                id TEXT PRIMARY KEY,
                task_id TEXT NOT NULL,
                started_at TEXT NOT NULL,
                ended_at TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS activity_intervals (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                work_session_id TEXT NOT NULL,
                source TEXT NOT NULL,
                started_at TEXT NOT NULL,
                ended_at TEXT NOT NULL,
                FOREIGN KEY (work_session_id) REFERENCES work_sessions (id)
            );

            CREATE TABLE IF NOT EXISTS worklogs (
                id TEXT PRIMARY KEY,
                work_session_id TEXT NOT NULL,
                task_id TEXT NOT NULL,
                human_minutes REAL NOT NULL,
                ai_minutes REAL NOT NULL,
                approved_at TEXT NULL,
                synced_at TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS app_settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);

        await EnsureTasksProviderIdColumnAsync(connection, cancellationToken);
    }

    /// <summary>
    /// Adds the <c>tasks.provider_id</c> column (introduced by #38, after #16 already shipped the
    /// table without it) on databases created by older app versions. <c>CREATE TABLE IF NOT
    /// EXISTS</c> alone can't evolve an existing table, so this checks <c>PRAGMA table_info</c>
    /// and runs <c>ALTER TABLE ... ADD COLUMN</c> only when the column is missing - idempotent,
    /// and a no-op on databases that already have it (freshly created ones included, since the
    /// <c>CREATE TABLE</c> above already declares it).
    /// </summary>
    private static async Task EnsureTasksProviderIdColumnAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var hasProviderIdColumn = false;

        await using (var pragmaCommand = connection.CreateCommand())
        {
            pragmaCommand.CommandText = "PRAGMA table_info('tasks');";

            await using var reader = await pragmaCommand.ExecuteReaderAsync(cancellationToken);
            var nameOrdinal = reader.GetOrdinal("name");
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(nameOrdinal), "provider_id", StringComparison.Ordinal))
                {
                    hasProviderIdColumn = true;
                    break;
                }
            }
        }

        if (hasProviderIdColumn)
        {
            return;
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = "ALTER TABLE tasks ADD COLUMN provider_id TEXT NULL;";
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
