using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Infrastructure.Tests.Monitoring;

/// <summary>
/// Hand-written fake for <see cref="IMonitoredActivityRepository"/>, no mocking library involved.
/// </summary>
internal sealed class FakeMonitoredActivityRepository : IMonitoredActivityRepository
{
    private readonly List<ActivityInterval> _activities = [];

    public IReadOnlyList<ActivityInterval> Activities => _activities;

    public Task AddAsync(ActivityInterval activity, CancellationToken cancellationToken)
    {
        lock (_activities)
        {
            _activities.Add(activity);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ActivityInterval>> ListByPeriodAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        lock (_activities)
        {
            IReadOnlyList<ActivityInterval> matches = _activities
                .Where(a => a.StartedAt <= to && a.EndedAt >= from)
                .OrderBy(a => a.StartedAt)
                .ToList();
            return Task.FromResult(matches);
        }
    }
}
