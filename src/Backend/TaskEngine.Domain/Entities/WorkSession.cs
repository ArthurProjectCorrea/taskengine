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
