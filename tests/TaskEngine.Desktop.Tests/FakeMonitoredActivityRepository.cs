using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Desktop.Tests;

/// <summary>Hand-written in-memory fake for <see cref="IMonitoredActivityRepository"/>, no mocking library.</summary>
public sealed class FakeMonitoredActivityRepository : IMonitoredActivityRepository
{
    private readonly List<ActivityInterval> _activities = [];

    public Task AddAsync(ActivityInterval activity, CancellationToken cancellationToken)
    {
        _activities.Add(activity);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ActivityInterval>> ListByPeriodAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        IReadOnlyList<ActivityInterval> matches = _activities
            .Where(a => a.StartedAt < to && a.EndedAt > from)
            .OrderBy(a => a.StartedAt)
            .ToList();
        return Task.FromResult(matches);
    }
}
