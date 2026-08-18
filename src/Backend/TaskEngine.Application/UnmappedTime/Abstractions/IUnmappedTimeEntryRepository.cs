using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.UnmappedTime.Abstractions;

/// <summary>
/// Port for persisting <see cref="UnmappedTimeEntry"/> instances. Implemented by
/// TaskEngine.Infrastructure.
/// </summary>
public interface IUnmappedTimeEntryRepository
{
    Task AddAsync(UnmappedTimeEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<UnmappedTimeEntry>> ListByTaskIdAsync(Guid taskId, CancellationToken cancellationToken);
}
