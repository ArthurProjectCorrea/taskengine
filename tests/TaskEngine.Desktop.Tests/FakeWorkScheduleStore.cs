using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Desktop.Tests;

/// <summary>Hand-written in-memory fake for <see cref="IWorkScheduleStore"/>, no mocking library.</summary>
public sealed class FakeWorkScheduleStore : IWorkScheduleStore
{
    private WorkSchedule? _schedule;

    public Task<WorkSchedule?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(_schedule);

    public Task SaveAsync(WorkSchedule schedule, CancellationToken cancellationToken)
    {
        _schedule = schedule;
        return Task.CompletedTask;
    }
}
