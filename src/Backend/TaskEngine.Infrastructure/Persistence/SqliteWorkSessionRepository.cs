using Microsoft.Data.Sqlite;
using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Infrastructure.Persistence;

/// <summary>
/// <see cref="IWorkSessionRepository"/> implementation backed by SQLite (<c>work_sessions</c> +
/// <c>activity_intervals</c> tables). On every write, activity intervals are replaced wholesale
/// (delete-then-reinsert) rather than diffed - simpler and correct even though
/// <see cref="WorkSession.ApplyActivitySelection"/> can now update an existing activity's
/// <see cref="ActivityInterval.SelectedAtConclusion"/> in place, since <see cref="ActivityInterval.Id"/>
/// is preserved on reinsert either way.
/// </summary>
public sealed class SqliteWorkSessionRepository : IWorkSessionRepository
{
    private readonly SqlitePathProvider _pathProvider;

    public SqliteWorkSessionRepository(SqlitePathProvider pathProvider)
    {
        _pathProvider = pathProvider;
    }

    public async Task AddAsync(WorkSession workSession, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO work_sessions (id, task_id, started_at, ended_at, type, origin)
                VALUES ($id, $taskId, $startedAt, $endedAt, $type, $origin);
                """;
            AddWorkSessionParameters(command, workSession);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertActivitiesAsync(connection, transaction, workSession, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateAsync(WorkSession workSession, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE work_sessions
                SET task_id = $taskId, started_at = $startedAt, ended_at = $endedAt, type = $type, origin = $origin
                WHERE id = $id;
                """;
            AddWorkSessionParameters(command, workSession);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM activity_intervals WHERE work_session_id = $workSessionId;";
            deleteCommand.Parameters.AddWithValue("$workSessionId", workSession.Id.ToString());
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertActivitiesAsync(connection, transaction, workSession, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<WorkSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        WorkSessionRow? row;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT id, task_id, started_at, ended_at, type, origin
                FROM work_sessions
                WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$id", id.ToString());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            row = await reader.ReadAsync(cancellationToken) ? ReadWorkSessionRow(reader) : null;
        }

        if (row is null)
        {
            return null;
        }

        var activities = await ListActivitiesAsync(connection, id, cancellationToken);
        return ToWorkSession(row, activities);
    }

    public async Task<IReadOnlyList<WorkSession>> ListByTaskIdAsync(Guid taskId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var rows = new List<WorkSessionRow>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT id, task_id, started_at, ended_at, type, origin
                FROM work_sessions
                WHERE task_id = $taskId
                ORDER BY started_at;
                """;
            command.Parameters.AddWithValue("$taskId", taskId.ToString());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(ReadWorkSessionRow(reader));
            }
        }

        var sessions = new List<WorkSession>(rows.Count);
        foreach (var row in rows)
        {
            var activities = await ListActivitiesAsync(connection, row.Id, cancellationToken);
            sessions.Add(ToWorkSession(row, activities));
        }

        return sessions;
    }

    private static async Task InsertActivitiesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        WorkSession workSession,
        CancellationToken cancellationToken)
    {
        foreach (var activity in workSession.Activities)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO activity_intervals
                    (work_session_id, source, started_at, ended_at, activity_id, type, path, selected_at_conclusion)
                VALUES
                    ($workSessionId, $source, $startedAt, $endedAt, $activityId, $type, $path, $selectedAtConclusion);
                """;
            command.Parameters.AddWithValue("$workSessionId", workSession.Id.ToString());
            command.Parameters.AddWithValue("$source", activity.Source.ToString());
            command.Parameters.AddWithValue("$startedAt", SqliteDateTimeOffsetFormat.ToText(activity.StartedAt));
            command.Parameters.AddWithValue("$endedAt", SqliteDateTimeOffsetFormat.ToText(activity.EndedAt));
            command.Parameters.AddWithValue("$activityId", activity.Id.ToString());
            command.Parameters.AddWithValue("$type", (object?)activity.Type?.ToString() ?? DBNull.Value);
            command.Parameters.AddWithValue("$path", (object?)activity.Path ?? DBNull.Value);
            command.Parameters.AddWithValue("$selectedAtConclusion", activity.SelectedAtConclusion ? 1 : 0);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<List<ActivityInterval>> ListActivitiesAsync(
        SqliteConnection connection,
        Guid workSessionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT source, started_at, ended_at, activity_id, type, path, selected_at_conclusion
            FROM activity_intervals
            WHERE work_session_id = $workSessionId
            ORDER BY started_at;
            """;
        command.Parameters.AddWithValue("$workSessionId", workSessionId.ToString());

        var activities = new List<ActivityInterval>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var source = Enum.Parse<ActivitySource>(reader.GetString(0));
            var startedAt = SqliteDateTimeOffsetFormat.Parse(reader.GetString(1));
            var endedAt = SqliteDateTimeOffsetFormat.Parse(reader.GetString(2));
            var id = reader.IsDBNull(3) ? (Guid?)null : Guid.Parse(reader.GetString(3));
            var type = reader.IsDBNull(4) ? (ActivityItemType?)null : Enum.Parse<ActivityItemType>(reader.GetString(4));
            var path = reader.IsDBNull(5) ? null : reader.GetString(5);
            var selectedAtConclusion = !reader.IsDBNull(6) && reader.GetInt64(6) != 0;
            activities.Add(new ActivityInterval(source, startedAt, endedAt, type, path, selectedAtConclusion, id));
        }

        return activities;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_pathProvider.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void AddWorkSessionParameters(SqliteCommand command, WorkSession workSession)
    {
        command.Parameters.AddWithValue("$id", workSession.Id.ToString());
        command.Parameters.AddWithValue("$taskId", workSession.TaskId.ToString());
        command.Parameters.AddWithValue("$startedAt", SqliteDateTimeOffsetFormat.ToText(workSession.StartedAt));
        command.Parameters.AddWithValue(
            "$endedAt",
            workSession.EndedAt is { } endedAt ? SqliteDateTimeOffsetFormat.ToText(endedAt) : DBNull.Value);
        command.Parameters.AddWithValue("$type", workSession.Type.ToString());
        command.Parameters.AddWithValue("$origin", workSession.Origin.ToString());
    }

    private static WorkSessionRow ReadWorkSessionRow(SqliteDataReader reader)
    {
        return new WorkSessionRow(
            Id: Guid.Parse(reader.GetString(0)),
            TaskId: Guid.Parse(reader.GetString(1)),
            StartedAt: SqliteDateTimeOffsetFormat.Parse(reader.GetString(2)),
            EndedAt: reader.IsDBNull(3) ? null : SqliteDateTimeOffsetFormat.Parse(reader.GetString(3)),
            Type: reader.IsDBNull(4) ? WorkSessionType.Active : Enum.Parse<WorkSessionType>(reader.GetString(4)),
            Origin: reader.IsDBNull(5) ? WorkSessionOrigin.System : Enum.Parse<WorkSessionOrigin>(reader.GetString(5)));
    }

    private static WorkSession ToWorkSession(WorkSessionRow row, IEnumerable<ActivityInterval> activities)
    {
        return WorkSession.Restore(row.Id, row.TaskId, row.StartedAt, row.EndedAt, activities, row.Type, row.Origin);
    }

    private sealed record WorkSessionRow(
        Guid Id,
        Guid TaskId,
        DateTimeOffset StartedAt,
        DateTimeOffset? EndedAt,
        WorkSessionType Type,
        WorkSessionOrigin Origin);
}
