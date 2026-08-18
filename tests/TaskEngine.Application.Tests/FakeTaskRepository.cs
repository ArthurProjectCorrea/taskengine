using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Tests;

/// <summary>
/// Hand-written fake for <see cref="ITaskRepository"/>, no mocking library involved.
/// </summary>
public sealed class FakeTaskRepository : ITaskRepository
{
    private readonly List<TaskItem> _tasks = [];

    public IReadOnlyList<TaskItem> Tasks => _tasks;

    public Task AddAsync(TaskItem task, CancellationToken cancellationToken)
    {
        _tasks.Add(task);
        return Task.CompletedTask;
    }

    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_tasks.FirstOrDefault(t => t.Id == id));
    }

    public Task<IReadOnlyList<TaskItem>> ListAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<TaskItem>>(_tasks.ToList());
    }
}
