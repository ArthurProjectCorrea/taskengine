namespace TaskEngine.Domain.Entities;

/// <summary>
/// A single start→stop cycle of work on a <see cref="TaskItem"/>, identified by
/// <see cref="TaskId"/> only (no navigation property, to keep entities decoupled). Doubles as the
/// domain representation of the "Período de Acompanhamento" from Schema-004 (ERS-Tarefas.md):
/// <see cref="Type"/> distinguishes actively-tracked time from a pause, and <see cref="Origin"/>
/// records whether the status change that opened it came from the system or was detected while
/// syncing with the provider.
/// </summary>
public sealed class WorkSession
{
    private readonly List<ActivityInterval> _activities = [];

    public Guid Id { get; }
    public Guid TaskId { get; }
    public WorkSessionType Type { get; }
    public WorkSessionOrigin Origin { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? EndedAt { get; private set; }
    public IReadOnlyList<ActivityInterval> Activities => _activities;

    public bool IsOpen => EndedAt is null;

    public TimeSpan HumanDuration => SumDuration(ActivitySource.Human);
    public TimeSpan AiDuration => SumDuration(ActivitySource.Ai);

    private WorkSession(Guid id, Guid taskId, DateTimeOffset startedAt, WorkSessionType type, WorkSessionOrigin origin)
    {
        Id = id;
        TaskId = taskId;
        StartedAt = startedAt;
        Type = type;
        Origin = origin;
    }

    public static WorkSession Start(
        Guid taskId,
        DateTimeOffset startedAt,
        WorkSessionType type = WorkSessionType.Active,
        WorkSessionOrigin origin = WorkSessionOrigin.System)
    {
        return new WorkSession(Guid.NewGuid(), taskId, startedAt, type, origin);
    }

    /// <summary>
    /// Rehydrates a <see cref="WorkSession"/> from persisted state, including its already
    /// recorded <paramref name="activities"/>. Unlike <see cref="Start"/> + <see cref="RecordActivity"/>,
    /// this does not re-validate "new object" invariants (e.g. closed-session guard) - the data
    /// was already validated when the entity was first created.
    /// </summary>
    public static WorkSession Restore(
        Guid id,
        Guid taskId,
        DateTimeOffset startedAt,
        DateTimeOffset? endedAt,
        IEnumerable<ActivityInterval> activities,
        WorkSessionType type = WorkSessionType.Active,
        WorkSessionOrigin origin = WorkSessionOrigin.System)
    {
        var session = new WorkSession(id, taskId, startedAt, type, origin)
        {
            EndedAt = endedAt,
        };
        session._activities.AddRange(activities);

        return session;
    }

    public void RecordActivity(
        ActivitySource source,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        ActivityItemType? type = null,
        string? path = null)
    {
        if (EndedAt is not null)
        {
            throw new InvalidOperationException("Cannot record activity on a closed work session.");
        }

        if (Type != WorkSessionType.Active)
        {
            throw new InvalidOperationException("Cannot record activity on a paused work session.");
        }

        _activities.Add(new ActivityInterval(source, startedAt, endedAt, type, path));
    }

    /// <summary>
    /// Attributes an already-monitored activity (ERS-Monitoramento.md Schema-001,
    /// <c>IMonitoredActivityRepository</c>) to this work session, retroactively, at task
    /// conclusion time - the bridge between RF-004's task-independent activity stream and RF-007's
    /// per-task selection (RN-005/RN-012). Unlike <see cref="RecordActivity"/> (used for activity
    /// captured live while the session is open), this accepts an already-built
    /// <see cref="ActivityInterval"/> - preserving its original <see cref="ActivityInterval.Id"/>,
    /// since callers match against monitored-activity ids, not freshly generated ones - and is
    /// allowed on an already-closed session, since conclusion is exactly when a past Active session
    /// (from an earlier work period, before a pause) gets attributed.
    /// </summary>
    /// <remarks>
    /// The caller finds candidates via an <em>overlap</em> query (any part of the activity falls
    /// inside this session's window - RF-004/CA-004.1 needs that to support retroactive
    /// association of activity that predates the task). But the activity's own
    /// <see cref="ActivityInterval.StartedAt"/>/<see cref="ActivityInterval.EndedAt"/> can span far
    /// outside that window (e.g. a browser tab left open for days, or a file also touched by other
    /// tasks) - storing it unclipped would credit the session with time it never actually covered
    /// (a 2h task attributing a 5-day-old file's full lifetime). Clip to
    /// [<see cref="StartedAt"/>, <see cref="EndedAt"/> ?? now] before storing, so only the portion
    /// that genuinely falls within this session's own active period is ever counted.
    /// </remarks>
    public void AttributeSelectedActivity(ActivityInterval activity)
    {
        if (Type != WorkSessionType.Active)
        {
            throw new InvalidOperationException("Cannot attribute activity to a paused work session.");
        }

        if (_activities.Any(a => a.Id == activity.Id))
        {
            // Already attributed (e.g. a second pass over the same period) - avoid a duplicate entry.
            return;
        }

        DateTimeOffset windowEnd = EndedAt ?? DateTimeOffset.UtcNow;
        DateTimeOffset clippedStart = activity.StartedAt < StartedAt ? StartedAt : activity.StartedAt;
        DateTimeOffset clippedEnd = activity.EndedAt > windowEnd ? windowEnd : activity.EndedAt;

        if (clippedEnd <= clippedStart)
        {
            // The overlap query guarantees some overlap existed, but clipping can still collapse
            // it to nothing at the boundary (e.g. activity ends exactly at StartedAt) - contributes
            // no time, so there's nothing to attribute.
            return;
        }

        _activities.Add(new ActivityInterval(
            activity.Source, clippedStart, clippedEnd, activity.Type, activity.Path,
            selectedAtConclusion: true, id: activity.Id));
    }

    /// <summary>
    /// Rewrites which activities count as selected at task conclusion time (RF-007/RN-012), by
    /// replacing each activity whose <see cref="ActivityInterval.Id"/> is in
    /// <paramref name="selectedActivityIds"/> with a copy marked
    /// <see cref="ActivityInterval.SelectedAtConclusion"/> = true, and every other activity with
    /// a copy marked false.
    /// </summary>
    public void ApplyActivitySelection(IReadOnlySet<Guid> selectedActivityIds)
    {
        for (var i = 0; i < _activities.Count; i++)
        {
            ActivityInterval activity = _activities[i];
            var selected = selectedActivityIds.Contains(activity.Id);
            if (activity.SelectedAtConclusion != selected)
            {
                _activities[i] = activity with { SelectedAtConclusion = selected };
            }
        }
    }

    public void End(DateTimeOffset endedAt)
    {
        if (EndedAt is not null)
        {
            throw new InvalidOperationException("Work session is already closed.");
        }

        if (endedAt < StartedAt)
        {
            throw new ArgumentException("EndedAt cannot be before StartedAt.", nameof(endedAt));
        }

        EndedAt = endedAt;
    }

    private TimeSpan SumDuration(ActivitySource source)
    {
        var total = TimeSpan.Zero;
        foreach (var activity in _activities)
        {
            if (activity.Source == source)
            {
                total += activity.Duration;
            }
        }

        return total;
    }
}
