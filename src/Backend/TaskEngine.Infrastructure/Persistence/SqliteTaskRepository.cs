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
            INSERT INTO tasks (id, title, description, status, provider_id, provider_task_id, created_at, provider_status_name, priority, provider_url)
            VALUES ($id, $title, $description, $status, $providerId, $providerTaskId, $createdAt, $providerStatusName, $priority, $providerUrl);
            """;

        command.Parameters.AddWithValue("$id", task.Id.ToString());
        command.Parameters.AddWithValue("$title", task.Title);
        command.Parameters.AddWithValue("$description", (object?)task.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", task.Status.ToString());
        command.Parameters.AddWithValue("$providerId", (object?)task.ProviderId ?? DBNull.Value);
        command.Parameters.AddWithValue("$providerTaskId", (object?)task.ProviderTaskId ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", SqliteDateTimeOffsetFormat.ToText(task.CreatedAt));
        command.Parameters.AddWithValue("$providerStatusName", (object?)task.ProviderStatusName ?? DBNull.Value);
        command.Parameters.AddWithValue("$priority", (object?)task.Priority ?? DBNull.Value);
        command.Parameters.AddWithValue("$providerUrl", (object?)task.ProviderUrl ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(TaskItem task, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE tasks
            SET title = $title, description = $description, status = $status,
                provider_id = $providerId, provider_task_id = $providerTaskId, created_at = $createdAt,
                provider_status_name = $providerStatusName, priority = $priority, provider_url = $providerUrl
            WHERE id = $id;
            """;

        command.Parameters.AddWithValue("$id", task.Id.ToString());
        command.Parameters.AddWithValue("$title", task.Title);
        command.Parameters.AddWithValue("$description", (object?)task.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", task.Status.ToString());
        command.Parameters.AddWithValue("$providerId", (object?)task.ProviderId ?? DBNull.Value);
        command.Parameters.AddWithValue("$providerTaskId", (object?)task.ProviderTaskId ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", SqliteDateTimeOffsetFormat.ToText(task.CreatedAt));
        command.Parameters.AddWithValue("$providerStatusName", (object?)task.ProviderStatusName ?? DBNull.Value);
        command.Parameters.AddWithValue("$priority", (object?)task.Priority ?? DBNull.Value);
        command.Parameters.AddWithValue("$providerUrl", (object?)task.ProviderUrl ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, title, description, status, provider_id, provider_task_id, created_at, provider_status_name, priority, provider_url
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
            SELECT id, title, description, status, provider_id, provider_task_id, created_at, provider_status_name, priority, provider_url
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
            providerId: reader.IsDBNull(4) ? null : reader.GetString(4),
            providerTaskId: reader.IsDBNull(5) ? null : reader.GetString(5),
            createdAt: SqliteDateTimeOffsetFormat.Parse(reader.GetString(6)),
            providerStatusName: reader.IsDBNull(7) ? null : reader.GetString(7),
            priority: reader.IsDBNull(8) ? null : reader.GetString(8),
            providerUrl: reader.IsDBNull(9) ? null : reader.GetString(9));
    }
}
