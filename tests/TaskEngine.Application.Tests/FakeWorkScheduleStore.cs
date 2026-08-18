using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Tests;

/// <summary>
/// Hand-written fake for <see cref="IWorkScheduleStore"/>, no mocking library involved.
/// </summary>
public sealed class FakeWorkScheduleStore : IWorkScheduleStore
{
    private WorkSchedule? _schedule;

    public Task<WorkSchedule?> GetAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_schedule);
    }

    public Task SaveAsync(WorkSchedule schedule, CancellationToken cancellationToken)
    {
        _schedule = schedule;
        return Task.CompletedTask;
    }
}
