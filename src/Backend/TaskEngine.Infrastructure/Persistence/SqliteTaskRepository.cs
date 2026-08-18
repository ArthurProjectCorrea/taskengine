using Microsoft.Data.Sqlite;
using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Infrastructure.Persistence;

/// <summary>
/// <see cref="ITaskRepository"/> implementation backed by SQLite (<c>tasks</c> table). Opens a
/// dedicated <see cref="SqliteConnection"/> per operation rather than holding one open.
/// </summary>
public sealed class SqliteTaskRepository : ITaskRepository
{
    private readonly SqlitePathProvider _pathProvider;

    public SqliteTaskRepository(SqlitePathProvider pathProvider)
    {
        _pathProvider = pathProvider;
    }

    public async Task AddAsync(TaskItem task, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO tasks (id, title, description, status, provider_task_id, created_at)
            VALUES ($id, $title, $description, $status, $providerTaskId, $createdAt);
            """;

        command.Parameters.AddWithValue("$id", task.Id.ToString());
        command.Parameters.AddWithValue("$title", task.Title);
        command.Parameters.AddWithValue("$description", (object?)task.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", task.Status.ToString());
        command.Parameters.AddWithValue("$providerTaskId", (object?)task.ProviderTaskId ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", SqliteDateTimeOffsetFormat.ToText(task.CreatedAt));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, title, description, status, provider_task_id, created_at
            FROM tasks
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadTask(reader);
    }

    public async Task<IReadOnlyList<TaskItem>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, title, description, status, provider_task_id, created_at
            FROM tasks
            ORDER BY created_at;
            """;

        var tasks = new List<TaskItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tasks.Add(ReadTask(reader));
        }

        return tasks;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_pathProvider.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static TaskItem ReadTask(SqliteDataReader reader)
    {
        return TaskItem.Restore(
            id: Guid.Parse(reader.GetString(0)),
            title: reader.GetString(1),
            description: reader.IsDBNull(2) ? null : reader.GetString(2),
            status: Enum.Parse<TaskStatus>(reader.GetString(3)),
            providerTaskId: reader.IsDBNull(4) ? null : reader.GetString(4),
            createdAt: SqliteDateTimeOffsetFormat.Parse(reader.GetString(5)));
    }
}
