using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Desktop.Tests;

/// <summary>Hand-written in-memory fake for <see cref="ITaskRepository"/>, no mocking library.</summary>
public sealed class FakeTaskRepository : ITaskRepository
{
    private readonly Dictionary<Guid, TaskItem> _tasks = [];

    public Task AddAsync(TaskItem task, CancellationToken cancellationToken)
    {
        _tasks[task.Id] = task;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TaskItem task, CancellationToken cancellationToken)
    {
        _tasks[task.Id] = task;
        return Task.CompletedTask;
    }

    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_tasks.GetValueOrDefault(id));
    }

    public Task<IReadOnlyList<TaskItem>> ListAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<TaskItem>>(_tasks.Values.ToList());
    }
}
