using TaskEngine.Application.Providers;
using TaskEngine.Domain.Entities;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Application.Abstractions;

/// <summary>
/// Port for a task provider (e.g. GitHub, Jira, ClickUp) capable of creating tasks and
/// synchronizing their status. Implemented by TaskEngine.Infrastructure.
/// </summary>
public interface ITaskProviderClient
{
    string ProviderId { get; }

    /// <summary>
    /// Fetches the fields this provider (and connected project/board) expects for task
    /// creation, so the task creation UI can render them dynamically.
    /// </summary>
    Task<ProviderTaskSchema> GetTaskSchemaAsync(CancellationToken cancellationToken);

    /// <summary>
    /// <paramref name="fieldValues"/> keys match <see cref="ProviderFieldDefinition.Key"/> from
    /// <see cref="GetTaskSchemaAsync"/>. For <see cref="ProviderFieldType.SingleSelect"/> fields
    /// the value must be the option's <see cref="ProviderFieldOption.Id"/>, not its display name.
    /// </summary>
    Task<ProviderTaskReference> CreateTaskAsync(
        TaskItem task,
        IReadOnlyDictionary<string, string>? fieldValues,
        CancellationToken cancellationToken);

    Task UpdateStatusAsync(ProviderTaskReference reference, TaskStatus status, CancellationToken cancellationToken);
}
