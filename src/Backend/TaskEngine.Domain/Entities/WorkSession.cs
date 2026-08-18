namespace TaskEngine.Domain.Entities;

/// <summary>
/// A single start→stop cycle of work on a <see cref="TaskItem"/>, identified by
/// <see cref="TaskId"/> only (no navigation property, to keep entities decoupled).
/// </summary>
public sealed class WorkSession
{
    private readonly List<ActivityInterval> _activities = [];

    public Guid Id { get; }
    public Guid TaskId { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? EndedAt { get; private set; }
    public IReadOnlyList<ActivityInterval> Activities => _activities;

    public bool IsOpen => EndedAt is null;

    public TimeSpan HumanDuration => SumDuration(ActivitySource.Human);
    public TimeSpan AiDuration => SumDuration(ActivitySource.Ai);

    private WorkSession(Guid id, Guid taskId, DateTimeOffset startedAt)
    {
        Id = id;
        TaskId = taskId;
        StartedAt = startedAt;
    }

    public static WorkSession Start(Guid taskId, DateTimeOffset startedAt)
    {
        return new WorkSession(Guid.NewGuid(), taskId, startedAt);
    }

    public void RecordActivity(ActivitySource source, DateTimeOffset startedAt, DateTimeOffset endedAt)
    {
        if (EndedAt is not null)
        {
            throw new InvalidOperationException("Cannot record activity on a closed work session.");
        }

        _activities.Add(new ActivityInterval(source, startedAt, endedAt));
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
