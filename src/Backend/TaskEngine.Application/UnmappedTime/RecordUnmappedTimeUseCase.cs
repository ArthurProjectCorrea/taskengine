using TaskEngine.Application.UnmappedTime.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.UnmappedTime;

/// <summary>
/// Manually records a block of unmapped time worked away from the computer. This is a manual
/// mechanism only - there is no restriction on the owning task being open or closed, and Domain
/// validation (duration/justification) is left to propagate as-is.
/// </summary>
public sealed class RecordUnmappedTimeUseCase
{
    private readonly IUnmappedTimeEntryRepository _unmappedTimeEntryRepository;

    public RecordUnmappedTimeUseCase(IUnmappedTimeEntryRepository unmappedTimeEntryRepository)
    {
        _unmappedTimeEntryRepository = unmappedTimeEntryRepository;
    }

    public async Task<UnmappedTimeEntryDto> ExecuteAsync(
        Guid taskId,
        TimeSpan duration,
        string justification,
        CancellationToken cancellationToken = default)
    {
        UnmappedTimeEntry entry = UnmappedTimeEntry.Create(taskId, duration, justification, DateTimeOffset.UtcNow);

        await _unmappedTimeEntryRepository.AddAsync(entry, cancellationToken);

        return new UnmappedTimeEntryDto(entry.Id, entry.TaskId, entry.Duration, entry.Justification, entry.RecordedAt);
    }
}
