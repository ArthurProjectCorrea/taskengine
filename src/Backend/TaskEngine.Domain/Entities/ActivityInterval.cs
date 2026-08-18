namespace TaskEngine.Domain.Entities;

/// <summary>
/// Immutable slice of activity within a <see cref="WorkSession"/>, attributed to either a
/// human or an AI agent.
/// </summary>
public sealed record ActivityInterval
{
    public ActivitySource Source { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset EndedAt { get; }

    public ActivityInterval(ActivitySource source, DateTimeOffset startedAt, DateTimeOffset endedAt)
    {
        if (endedAt <= startedAt)
        {
            throw new ArgumentException("EndedAt must be after StartedAt.", nameof(endedAt));
        }

        Source = source;
        StartedAt = startedAt;
        EndedAt = endedAt;
    }

    public TimeSpan Duration => EndedAt - StartedAt;
}
