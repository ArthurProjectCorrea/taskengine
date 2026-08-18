using TaskEngine.Application.Abstractions;
using TaskEngine.Application.UnmappedTime.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.UnmappedTime;

/// <summary>
/// Calculates how much of a task's scheduled time is not yet accounted for by recorded
/// <see cref="WorkSession"/> activity or already-recorded <see cref="UnmappedTimeEntry"/>
/// entries. Always returns the raw calculated value - <see cref="MinimumPromptThreshold"/> is a
/// UX decision about when it is worth prompting the user, left to the presentation layer.
/// </summary>
public sealed class CalculateUnmappedTimeUseCase
{
    /// <summary>
    /// Below this amount, presentation layers should not bother prompting the user about
    /// unmapped time - it is not a calculation constraint.
    /// </summary>
    public static readonly TimeSpan MinimumPromptThreshold = TimeSpan.FromMinutes(15);

    private readonly IWorkSessionRepository _workSessionRepository;
    private readonly IWorkScheduleStore _workScheduleStore;
    private readonly IUnmappedTimeEntryRepository _unmappedTimeEntryRepository;

    public CalculateUnmappedTimeUseCase(
        IWorkSessionRepository workSessionRepository,
        IWorkScheduleStore workScheduleStore,
        IUnmappedTimeEntryRepository unmappedTimeEntryRepository)
    {
        _workSessionRepository = workSessionRepository;
        _workScheduleStore = workScheduleStore;
        _unmappedTimeEntryRepository = unmappedTimeEntryRepository;
    }

    public async Task<TimeSpan?> ExecuteAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WorkSession> sessions = await _workSessionRepository.ListByTaskIdAsync(taskId, cancellationToken);
        if (sessions.Count == 0)
        {
            return TimeSpan.Zero;
        }

        WorkSchedule? schedule = await _workScheduleStore.GetAsync(cancellationToken);
        if (schedule is null)
        {
            return null;
        }

        DateTimeOffset? startedAt = null;
        DateTimeOffset? endedAt = null;
        var mappedSeconds = 0d;

        foreach (WorkSession session in sessions)
        {
            if (startedAt is null || session.StartedAt < startedAt)
            {
                startedAt = session.StartedAt;
            }

            if (session.EndedAt is { } sessionEndedAt && (endedAt is null || sessionEndedAt > endedAt))
            {
                endedAt = sessionEndedAt;
            }

            mappedSeconds += session.AiDuration.TotalSeconds + session.HumanDuration.TotalSeconds;
        }

        if (endedAt is null)
        {
            return TimeSpan.Zero;
        }

        TimeSpan scheduledHours = schedule.CalculateHoursInPeriod(
            DateOnly.FromDateTime(startedAt!.Value.UtcDateTime),
            DateOnly.FromDateTime(endedAt.Value.UtcDateTime));

        IReadOnlyList<UnmappedTimeEntry> existingEntries =
            await _unmappedTimeEntryRepository.ListByTaskIdAsync(taskId, cancellationToken);
        var alreadyRecordedSeconds = existingEntries.Sum(entry => entry.Duration.TotalSeconds);

        var remainingSeconds = Math.Max(0, scheduledHours.TotalSeconds - mappedSeconds - alreadyRecordedSeconds);
        return TimeSpan.FromSeconds(remainingSeconds);
    }
}
