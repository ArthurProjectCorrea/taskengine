namespace TaskEngine.Domain.TimeTracking;

/// <summary>
/// A plain start/end time range, decoupled from any specific entity (<see cref="Entities.ActivityInterval"/>,
/// <see cref="Entities.WorkSession"/>, etc.) so it can be merged generically by
/// <see cref="TimeIntervalMerger"/>.
/// </summary>
public readonly record struct TimeInterval
{
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }

    public TimeInterval(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            throw new ArgumentException("End must be after Start.", nameof(end));
        }

        Start = start;
        End = end;
    }

    public TimeSpan Duration => End - Start;
}
