using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Abstractions;

/// <summary>
/// Port for the continuous, task-independent activity history captured by the monitoring module
/// (ERS-Monitoramento.md, RN-001: monitoring runs whenever the system is running, whether or not a
/// task is in progress). Distinct from <see cref="IWorkSessionRepository"/>, which persists
/// <see cref="ActivityInterval"/>s already attributed to a specific <c>WorkSession</c>/task -
/// entries recorded here have no task yet and are made available for retroactive association
/// (RF-004) via <see cref="ListByPeriodAsync"/>.
/// </summary>
public interface IMonitoredActivityRepository
{
    Task AddAsync(ActivityInterval activity, CancellationToken cancellationToken);

    /// <summary>
    /// Returns every recorded activity whose interval overlaps <paramref name="from"/>..<paramref name="to"/>,
    /// ordered by <see cref="ActivityInterval.StartedAt"/> - the query behind RF-004 (recovering
    /// activity for a task's period even when that period predates the task's own creation).
    /// </summary>
    Task<IReadOnlyList<ActivityInterval>> ListByPeriodAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
