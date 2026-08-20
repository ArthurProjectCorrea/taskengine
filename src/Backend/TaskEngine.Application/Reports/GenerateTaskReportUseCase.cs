using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;
using TaskEngine.Domain.TimeTracking;

namespace TaskEngine.Application.Reports;

/// <summary>
/// Builds the general consolidated task report (RF-016/RN-007/RN-013, ERS-Tarefas.md): one
/// <see cref="TaskReportRow"/> per synced task, aggregating its <see cref="WorkSession"/> data,
/// expedient (<see cref="IWorkScheduleStore"/>) and unmapped time
/// (<see cref="IUnmappedTimeEntryRepository"/>) - exclusively from data already saved locally, per
/// RN-013 (no provider call happens here). Formatting (CSV or otherwise) is a separate concern -
/// see <see cref="CsvTaskReportWriter"/>.
/// </summary>
public sealed class GenerateTaskReportUseCase
{
    private readonly ITaskRepository _taskRepository;
    private readonly IWorkSessionRepository _workSessionRepository;
    private readonly IWorkScheduleStore _workScheduleStore;
    private readonly IUnmappedTimeEntryRepository _unmappedTimeEntryRepository;

    public GenerateTaskReportUseCase(
        ITaskRepository taskRepository,
        IWorkSessionRepository workSessionRepository,
        IWorkScheduleStore workScheduleStore,
        IUnmappedTimeEntryRepository unmappedTimeEntryRepository)
    {
        _taskRepository = taskRepository;
        _workSessionRepository = workSessionRepository;
        _workScheduleStore = workScheduleStore;
        _unmappedTimeEntryRepository = unmappedTimeEntryRepository;
    }

    /// <summary>
    /// <paramref name="periodStart"/>/<paramref name="periodEnd"/> implement the optional period
    /// filter from CA-016.3: when either is given, only tasks whose own period (see
    /// <see cref="TaskReportRow.StartedAt"/>/<see cref="TaskReportRow.EndedAt"/>) overlaps
    /// [<paramref name="periodStart"/>, <paramref name="periodEnd"/>] are returned - a task with no
    /// sessions at all never matches an active filter, and a still-open task (null
    /// <see cref="TaskReportRow.EndedAt"/>) is treated as extending indefinitely past its start.
    /// With both left <c>null</c>, every synced task is returned (CA-016.1).
    /// </summary>
    public async Task<IReadOnlyList<TaskReportRow>> ExecuteAsync(
        DateOnly? periodStart = null,
        DateOnly? periodEnd = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TaskItem> tasks = await _taskRepository.ListAsync(cancellationToken);
        WorkSchedule? schedule = await _workScheduleStore.GetAsync(cancellationToken);

        var rows = new List<TaskReportRow>(tasks.Count);
        foreach (TaskItem task in tasks)
        {
            IReadOnlyList<WorkSession> sessions = await _workSessionRepository.ListByTaskIdAsync(task.Id, cancellationToken);
            IReadOnlyList<UnmappedTimeEntry> unmappedEntries =
                await _unmappedTimeEntryRepository.ListByTaskIdAsync(task.Id, cancellationToken);

            TaskReportRow row = BuildRow(task, sessions, unmappedEntries, schedule);
            if (MatchesPeriod(row.StartedAt, row.EndedAt, periodStart, periodEnd))
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private static bool MatchesPeriod(
        DateOnly? taskStart, DateOnly? taskEnd, DateOnly? periodStart, DateOnly? periodEnd)
    {
        if (periodStart is null && periodEnd is null)
        {
            return true;
        }

        if (taskStart is null)
        {
            // No session data at all - there is no period to compare against the filter.
            return false;
        }

        var startsBeforePeriodEnds = periodEnd is null || taskStart <= periodEnd;
        var endsAfterPeriodStarts = periodStart is null || (taskEnd ?? DateOnly.MaxValue) >= periodStart;
        return startsBeforePeriodEnds && endsAfterPeriodStarts;
    }

    private static TaskReportRow BuildRow(
        TaskItem task,
        IReadOnlyList<WorkSession> sessions,
        IReadOnlyList<UnmappedTimeEntry> unmappedEntries,
        WorkSchedule? schedule)
    {
        DateOnly? startedAt = null;
        DateOnly? endedAt = null;
        var humanIntervals = new List<TimeInterval>();
        var aiIntervals = new List<TimeInterval>();
        var allIntervals = new List<TimeInterval>();

        foreach (WorkSession session in sessions)
        {
            var sessionStartedAt = DateOnly.FromDateTime(session.StartedAt.UtcDateTime);
            if (startedAt is null || sessionStartedAt < startedAt)
            {
                startedAt = sessionStartedAt;
            }

            if (session.EndedAt is { } sessionEndedAt)
            {
                var sessionEndedOn = DateOnly.FromDateTime(sessionEndedAt.UtcDateTime);
                if (endedAt is null || sessionEndedOn > endedAt)
                {
                    endedAt = sessionEndedOn;
                }
            }

            if (session.Type != WorkSessionType.Active)
            {
                // A paused session never has recorded activities (WorkSession.RecordActivity
                // guards against it) - skipped here mainly for clarity/RN-012 intent.
                continue;
            }

            foreach (ActivityInterval activity in session.Activities)
            {
                var interval = new TimeInterval(activity.StartedAt, activity.EndedAt);
                allIntervals.Add(interval);

                List<TimeInterval> bucket = activity.Source == ActivitySource.Human ? humanIntervals : aiIntervals;
                bucket.Add(interval);
            }
        }

        // RN-007: overlapping activity - whether within the same origin (e.g. two files edited in
        // the same window) or between human and AI - is never double-counted.
        var humanSeconds = TimeIntervalMerger.TotalDuration(humanIntervals).TotalSeconds;
        var aiSeconds = TimeIntervalMerger.TotalDuration(aiIntervals).TotalSeconds;
        var totalSeconds = TimeIntervalMerger.TotalDuration(allIntervals).TotalSeconds;

        var scheduledSeconds = 0d;
        if (schedule is not null && startedAt is not null && endedAt is not null)
        {
            scheduledSeconds = schedule.CalculateHoursInPeriod(startedAt.Value, endedAt.Value).TotalSeconds;
        }

        var unmappedSeconds = unmappedEntries.Sum(entry => entry.Duration.TotalSeconds);
        IReadOnlyList<string> unmappedJustifications = unmappedEntries.Select(entry => entry.Justification).ToList();

        return new TaskReportRow(
            task.Id,
            startedAt,
            endedAt,
            task.ProviderId,
            aiSeconds,
            humanSeconds,
            scheduledSeconds,
            unmappedSeconds,
            unmappedJustifications,
            totalSeconds);
    }
}
