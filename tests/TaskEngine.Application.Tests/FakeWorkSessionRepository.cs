using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Tests;

/// <summary>
/// Hand-written fake for <see cref="IWorkSessionRepository"/>, no mocking library involved.
/// </summary>
public sealed class FakeWorkSessionRepository : IWorkSessionRepository
{
    private readonly List<WorkSession> _sessions = [];

    public IReadOnlyList<WorkSession> Sessions => _sessions;

    public Task AddAsync(WorkSession workSession, CancellationToken cancellationToken)
    {
        _sessions.Add(workSession);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(WorkSession workSession, CancellationToken cancellationToken)
    {
        var index = _sessions.FindIndex(s => s.Id == workSession.Id);
        if (index >= 0)
        {
            _sessions[index] = workSession;
        }

        return Task.CompletedTask;
    }

    public Task<WorkSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_sessions.FirstOrDefault(s => s.Id == id));
    }

    public Task<IReadOnlyList<WorkSession>> ListByTaskIdAsync(Guid taskId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<WorkSession>>(_sessions.Where(s => s.TaskId == taskId).ToList());
    }
}
