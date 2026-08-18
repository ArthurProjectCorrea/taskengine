using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Abstractions;

/// <summary>
/// Port for persisting <see cref="WorkSession"/> instances. Implemented by TaskEngine.Infrastructure.
/// </summary>
public interface IWorkSessionRepository
{
    Task AddAsync(WorkSession workSession, CancellationToken cancellationToken);

    Task UpdateAsync(WorkSession workSession, CancellationToken cancellationToken);

    Task<WorkSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkSession>> ListByTaskIdAsync(Guid taskId, CancellationToken cancellationToken);
}
