using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Desktop.Tests;

/// <summary>Hand-written in-memory fake for <see cref="IUnmappedTimeEntryRepository"/>, no mocking library.</summary>
public sealed class FakeUnmappedTimeEntryRepository : IUnmappedTimeEntryRepository
{
    private readonly List<UnmappedTimeEntry> _entries = [];

    public Task AddAsync(UnmappedTimeEntry entry, CancellationToken cancellationToken)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<UnmappedTimeEntry>> ListByTaskIdAsync(Guid taskId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<UnmappedTimeEntry>>(_entries.Where(e => e.TaskId == taskId).ToList());
    }
}
