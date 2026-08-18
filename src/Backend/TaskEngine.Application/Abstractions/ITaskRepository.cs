using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Abstractions;

/// <summary>
/// Port for persisting <see cref="TaskItem"/> instances. Implemented by TaskEngine.Infrastructure.
/// </summary>
public interface ITaskRepository
{
    Task AddAsync(TaskItem task, CancellationToken cancellationToken);

    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskItem>> ListAsync(CancellationToken cancellationToken);
}
