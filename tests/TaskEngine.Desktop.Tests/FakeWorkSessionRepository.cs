using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Desktop.Tests;

/// <summary>Hand-written in-memory fake for <see cref="IWorkSessionRepository"/>, no mocking library.</summary>
public sealed class FakeWorkSessionRepository : IWorkSessionRepository
{
    private readonly Dictionary<Guid, WorkSession> _sessions = [];

    public Task AddAsync(WorkSession workSession, CancellationToken cancellationToken)
    {
        _sessions[workSession.Id] = workSession;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(WorkSession workSession, CancellationToken cancellationToken)
    {
        _sessions[workSession.Id] = workSession;
        return Task.CompletedTask;
    }

    public Task<WorkSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_sessions.GetValueOrDefault(id));
    }

    public Task<IReadOnlyList<WorkSession>> ListByTaskIdAsync(Guid taskId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<WorkSession>>(_sessions.Values.Where(s => s.TaskId == taskId).ToList());
    }
}
