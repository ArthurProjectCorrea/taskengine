using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Abstractions;

/// <summary>
/// Port for persisting <see cref="Worklog"/> instances. Implemented by TaskEngine.Infrastructure.
/// </summary>
public interface IWorklogRepository
{
    Task AddAsync(Worklog worklog, CancellationToken cancellationToken);

    Task UpdateAsync(Worklog worklog, CancellationToken cancellationToken);

    Task<Worklog?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Worklog>> ListAsync(CancellationToken cancellationToken);
}
