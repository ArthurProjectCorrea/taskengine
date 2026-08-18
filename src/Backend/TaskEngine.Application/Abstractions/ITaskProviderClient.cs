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

    Task<ProviderTaskReference> CreateTaskAsync(TaskItem task, CancellationToken cancellationToken);

    Task UpdateStatusAsync(ProviderTaskReference reference, TaskStatus status, CancellationToken cancellationToken);
}
