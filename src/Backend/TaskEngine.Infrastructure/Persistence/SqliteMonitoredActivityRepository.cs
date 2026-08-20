using Microsoft.Data.Sqlite;
using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Infrastructure.Persistence;

/// <summary>
/// <see cref="IMonitoredActivityRepository"/> backed by SQLite (<c>monitored_activities</c>
/// table) - the task-independent activity history required by RN-001, queried by period for
/// retroactive association (RF-004).
/// </summary>
public sealed class SqliteMonitoredActivityRepository : IMonitoredActivityRepository
{
    private readonly SqlitePathProvider _pathProvider;

    public SqliteMonitoredActivityRepository(SqlitePathProvider pathProvider)
    {
        _pathProvider = pathProvider;
    }

    public async Task AddAsync(ActivityInterval activity, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_pathProvider.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO monitored_activities (id, source, started_at, ended_at, type, path)
            VALUES ($id, $source, $startedAt, $endedAt, $type, $path);
            """;
        command.Parameters.AddWithValue("$id", activity.Id.ToString());
        command.Parameters.AddWithValue("$source", activity.Source.ToString());
        command.Parameters.AddWithValue("$startedAt", SqliteDateTimeOffsetFormat.ToText(activity.StartedAt));
        command.Parameters.AddWithValue("$endedAt", SqliteDateTimeOffsetFormat.ToText(activity.EndedAt));
        command.Parameters.AddWithValue("$type", (object?)activity.Type?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$path", (object?)activity.Path ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ActivityInterval>> ListByPeriodAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_pathProvider.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        // Overlap test: the activity must start before the period ends and end after the period
        // starts - this also covers activity whose period predates the task the caller is
        // associating it with (RF-004/CA-004.1).
        command.CommandText = """
            SELECT id, source, started_at, ended_at, type, path
            FROM monitored_activities
            WHERE started_at <= $to AND ended_at >= $from
            ORDER BY started_at;
            """;
        command.Parameters.AddWithValue("$from", SqliteDateTimeOffsetFormat.ToText(from));
        command.Parameters.AddWithValue("$to", SqliteDateTimeOffsetFormat.ToText(to));

        var activities = new List<ActivityInterval>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = Guid.Parse(reader.GetString(0));
            var source = Enum.Parse<ActivitySource>(reader.GetString(1));
            var startedAt = SqliteDateTimeOffsetFormat.Parse(reader.GetString(2));
            var endedAt = SqliteDateTimeOffsetFormat.Parse(reader.GetString(3));
            var type = reader.IsDBNull(4) ? (ActivityItemType?)null : Enum.Parse<ActivityItemType>(reader.GetString(4));
            var path = reader.IsDBNull(5) ? null : reader.GetString(5);
            activities.Add(new ActivityInterval(source, startedAt, endedAt, type, path, id: id));
        }

        return activities;
    }
}
