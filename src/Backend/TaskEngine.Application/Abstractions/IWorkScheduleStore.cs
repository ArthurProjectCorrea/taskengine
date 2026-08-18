using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Abstractions;

/// <summary>
/// Port for persisting the single, current <see cref="WorkSchedule"/> template. Implemented by
/// TaskEngine.Infrastructure.
/// </summary>
public interface IWorkScheduleStore
{
    Task<WorkSchedule?> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(WorkSchedule schedule, CancellationToken cancellationToken);
}
