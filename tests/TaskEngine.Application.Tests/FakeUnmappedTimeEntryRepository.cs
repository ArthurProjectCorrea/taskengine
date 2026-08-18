using TaskEngine.Application.UnmappedTime.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Tests;

/// <summary>
/// Hand-written fake for <see cref="IUnmappedTimeEntryRepository"/>, no mocking library involved.
/// </summary>
public sealed class FakeUnmappedTimeEntryRepository : IUnmappedTimeEntryRepository
{
    private readonly List<UnmappedTimeEntry> _entries = [];

    public IReadOnlyList<UnmappedTimeEntry> Entries => _entries;

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
