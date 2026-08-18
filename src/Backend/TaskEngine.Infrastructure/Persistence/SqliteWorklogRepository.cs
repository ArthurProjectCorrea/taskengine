using Microsoft.Data.Sqlite;
using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Infrastructure.Persistence;

/// <summary>
/// <see cref="IWorklogRepository"/> implementation backed by SQLite (<c>worklogs</c> table).
/// </summary>
public sealed class SqliteWorklogRepository : IWorklogRepository
{
    private readonly SqlitePathProvider _pathProvider;

    public SqliteWorklogRepository(SqlitePathProvider pathProvider)
    {
        _pathProvider = pathProvider;
    }

    public async Task AddAsync(Worklog worklog, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO worklogs (id, work_session_id, task_id, human_minutes, ai_minutes, approved_at, synced_at)
            VALUES ($id, $workSessionId, $taskId, $humanMinutes, $aiMinutes, $approvedAt, $syncedAt);
            """;
        AddWorklogParameters(command, worklog);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Worklog worklog, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE worklogs
            SET work_session_id = $workSessionId,
                task_id = $taskId,
                human_minutes = $humanMinutes,
                ai_minutes = $aiMinutes,
                approved_at = $approvedAt,
                synced_at = $syncedAt
            WHERE id = $id;
            """;
        AddWorklogParameters(command, worklog);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Worklog?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, work_session_id, task_id, human_minutes, ai_minutes, approved_at, synced_at
            FROM worklogs
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadWorklog(reader);
    }

    public async Task<IReadOnlyList<Worklog>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, work_session_id, task_id, human_minutes, ai_minutes, approved_at, synced_at
            FROM worklogs;
            """;

        var worklogs = new List<Worklog>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            worklogs.Add(ReadWorklog(reader));
        }

        return worklogs;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_pathProvider.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void AddWorklogParameters(SqliteCommand command, Worklog worklog)
    {
        command.Parameters.AddWithValue("$id", worklog.Id.ToString());
        command.Parameters.AddWithValue("$workSessionId", worklog.WorkSessionId.ToString());
        command.Parameters.AddWithValue("$taskId", worklog.TaskId.ToString());
        command.Parameters.AddWithValue("$humanMinutes", worklog.HumanMinutes);
        command.Parameters.AddWithValue("$aiMinutes", worklog.AiMinutes);
        command.Parameters.AddWithValue(
            "$approvedAt",
            worklog.ApprovedAt is { } approvedAt ? SqliteDateTimeOffsetFormat.ToText(approvedAt) : DBNull.Value);
        command.Parameters.AddWithValue(
            "$syncedAt",
            worklog.SyncedAt is { } syncedAt ? SqliteDateTimeOffsetFormat.ToText(syncedAt) : DBNull.Value);
    }

    private static Worklog ReadWorklog(SqliteDataReader reader)
    {
        return Worklog.Restore(
            id: Guid.Parse(reader.GetString(0)),
            workSessionId: Guid.Parse(reader.GetString(1)),
            taskId: Guid.Parse(reader.GetString(2)),
            humanMinutes: reader.GetDouble(3),
            aiMinutes: reader.GetDouble(4),
            approvedAt: reader.IsDBNull(5) ? null : SqliteDateTimeOffsetFormat.Parse(reader.GetString(5)),
            syncedAt: reader.IsDBNull(6) ? null : SqliteDateTimeOffsetFormat.Parse(reader.GetString(6)));
    }
}
