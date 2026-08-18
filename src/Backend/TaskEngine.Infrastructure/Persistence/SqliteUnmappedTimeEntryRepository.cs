using Microsoft.Data.Sqlite;
using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Infrastructure.Persistence;

/// <summary>
/// <see cref="IUnmappedTimeEntryRepository"/> implementation backed by SQLite
/// (<c>unmapped_time_entries</c> table).
/// </summary>
public sealed class SqliteUnmappedTimeEntryRepository : IUnmappedTimeEntryRepository
{
    private readonly SqlitePathProvider _pathProvider;

    public SqliteUnmappedTimeEntryRepository(SqlitePathProvider pathProvider)
    {
        _pathProvider = pathProvider;
    }

    public async Task AddAsync(UnmappedTimeEntry entry, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO unmapped_time_entries (id, task_id, duration_seconds, justification, recorded_at)
            VALUES ($id, $taskId, $durationSeconds, $justification, $recordedAt);
            """;
        command.Parameters.AddWithValue("$id", entry.Id.ToString());
        command.Parameters.AddWithValue("$taskId", entry.TaskId.ToString());
        command.Parameters.AddWithValue("$durationSeconds", entry.Duration.TotalSeconds);
        command.Parameters.AddWithValue("$justification", entry.Justification);
        command.Parameters.AddWithValue("$recordedAt", SqliteDateTimeOffsetFormat.ToText(entry.RecordedAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UnmappedTimeEntry>> ListByTaskIdAsync(Guid taskId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, task_id, duration_seconds, justification, recorded_at
            FROM unmapped_time_entries
            WHERE task_id = $taskId
            ORDER BY recorded_at;
            """;
        command.Parameters.AddWithValue("$taskId", taskId.ToString());

        var entries = new List<UnmappedTimeEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(ReadEntry(reader));
        }

        return entries;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_pathProvider.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static UnmappedTimeEntry ReadEntry(SqliteDataReader reader)
    {
        return UnmappedTimeEntry.Restore(
            id: Guid.Parse(reader.GetString(0)),
            taskId: Guid.Parse(reader.GetString(1)),
            duration: TimeSpan.FromSeconds(reader.GetDouble(2)),
            justification: reader.GetString(3),
            recordedAt: SqliteDateTimeOffsetFormat.Parse(reader.GetString(4)));
    }
}
