namespace TaskEngine.Domain.Entities;

/// <summary>
/// A fixed weekly work-hours template: daily start/end time, an optional lunch break, and the
/// set of week days the template applies to. Used to calculate expected/scheduled hours over a
/// date range (see <see cref="CalculateHoursInPeriod"/>), independent of any actually recorded
/// <see cref="WorkSession"/>.
/// </summary>
public sealed class WorkSchedule
{
    public TimeOnly StartTime { get; }
    public TimeOnly EndTime { get; }
    public TimeOnly? LunchStart { get; }
    public TimeOnly? LunchEnd { get; }
    public IReadOnlySet<DayOfWeek> WorkDays { get; }

    private WorkSchedule(
        TimeOnly startTime,
        TimeOnly endTime,
        TimeOnly? lunchStart,
        TimeOnly? lunchEnd,
        HashSet<DayOfWeek> workDays)
    {
        StartTime = startTime;
        EndTime = endTime;
        LunchStart = lunchStart;
        LunchEnd = lunchEnd;
        WorkDays = workDays;
    }

    public static WorkSchedule Create(
        TimeOnly startTime,
        TimeOnly endTime,
        TimeOnly? lunchStart,
        TimeOnly? lunchEnd,
        IEnumerable<DayOfWeek> workDays)
    {
        if (endTime <= startTime)
        {
            throw new ArgumentException("EndTime must be after StartTime.", nameof(endTime));
        }

        if (lunchStart.HasValue != lunchEnd.HasValue)
        {
            throw new ArgumentException(
                "LunchStart and LunchEnd must be both set or both unset.",
                nameof(lunchStart));
        }

        if (lunchStart.HasValue && lunchEnd.HasValue)
        {
            if (lunchEnd.Value <= lunchStart.Value)
            {
                throw new ArgumentException("LunchEnd must be after LunchStart.", nameof(lunchEnd));
            }

            if (lunchStart.Value < startTime || lunchEnd.Value > endTime)
            {
                throw new ArgumentException(
                    "Lunch break must be within StartTime and EndTime.",
                    nameof(lunchStart));
            }
        }

        var normalizedWorkDays = new HashSet<DayOfWeek>(workDays);
        if (normalizedWorkDays.Count == 0)
        {
            throw new ArgumentException("WorkDays cannot be empty.", nameof(workDays));
        }

        return new WorkSchedule(startTime, endTime, lunchStart, lunchEnd, normalizedWorkDays);
    }

    /// <summary>
    /// Sums the scheduled work hours for each day between <paramref name="start"/> and
    /// <paramref name="end"/> (inclusive) whose <see cref="DateOnly.DayOfWeek"/> is in
    /// <see cref="WorkDays"/>. Days outside the template count zero. Hours are not fractioned by
    /// time of day - a matching day always counts the full daily expedient, regardless of when in
    /// the range it falls.
    /// </summary>
    public TimeSpan CalculateHoursInPeriod(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new ArgumentException("End cannot be before start.", nameof(end));
        }

        var dailyHours = (EndTime - StartTime) - LunchDuration();

        var total = TimeSpan.Zero;
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (WorkDays.Contains(day.DayOfWeek))
            {
                total += dailyHours;
            }
        }

        return total;
    }

    private TimeSpan LunchDuration()
    {
        return LunchStart.HasValue && LunchEnd.HasValue
            ? LunchEnd.Value - LunchStart.Value
            : TimeSpan.Zero;
    }
}
