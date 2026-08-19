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
                ended_at TEXT NULL,
                type TEXT NULL,
                origin TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS activity_intervals (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                work_session_id TEXT NOT NULL,
                source TEXT NOT NULL,
                started_at TEXT NOT NULL,
                ended_at TEXT NOT NULL,
                activity_id TEXT NULL,
                type TEXT NULL,
                path TEXT NULL,
                selected_at_conclusion INTEGER NULL,
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

            CREATE TABLE IF NOT EXISTS unmapped_time_entries (
                id TEXT PRIMARY KEY,
                task_id TEXT NOT NULL,
                duration_seconds REAL NOT NULL,
                justification TEXT NOT NULL,
                recorded_at TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);

        await EnsureColumnAsync(connection, "tasks", "provider_id", "TEXT NULL", cancellationToken);

        // provider_status_name (raw provider status label, RN-010) and work_sessions.type/origin
        // (Schema-004 "Período de Acompanhamento" - pause/resume, #48) are new columns on tables
        // that already existed before this change, so they also need the ALTER TABLE path even
        // though the CREATE TABLE above already declares them for fresh databases.
        await EnsureColumnAsync(connection, "tasks", "provider_status_name", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "work_sessions", "type", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "work_sessions", "origin", "TEXT NULL", cancellationToken);

        // activity_intervals.activity_id/type/path/selected_at_conclusion: file/URL identity for
        // Schema-003 ("Item de Atividade") - supports the activity selection UI (RF-007) built in
        // a later change.
        await EnsureColumnAsync(connection, "activity_intervals", "activity_id", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "activity_intervals", "type", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "activity_intervals", "path", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "activity_intervals", "selected_at_conclusion", "INTEGER NULL", cancellationToken);

        // tasks.priority (Schema-001 "Prioridade") - populated from the provider's dynamic schema.
        await EnsureColumnAsync(connection, "tasks", "priority", "TEXT NULL", cancellationToken);
    }

    /// <summary>
    /// Adds <paramref name="column"/> to <paramref name="table"/> on databases created by an
    /// older app version, where the column didn't exist yet. <c>CREATE TABLE IF NOT EXISTS</c>
    /// alone can't evolve an existing table, so this checks <c>PRAGMA table_info</c> and runs
    /// <c>ALTER TABLE ... ADD COLUMN</c> only when the column is missing - idempotent, and a
    /// no-op on databases that already have it (freshly created ones included, since the
    /// <c>CREATE TABLE</c> above already declares it).
    /// </summary>
    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string table,
        string column,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        var hasColumn = false;

        await using (var pragmaCommand = connection.CreateCommand())
        {
            pragmaCommand.CommandText = $"PRAGMA table_info('{table}');";

            await using var reader = await pragmaCommand.ExecuteReaderAsync(cancellationToken);
            var nameOrdinal = reader.GetOrdinal("name");
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(nameOrdinal), column, StringComparison.Ordinal))
                {
                    hasColumn = true;
                    break;
                }
            }
        }

        if (hasColumn)
        {
            return;
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {columnDefinition};";
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
