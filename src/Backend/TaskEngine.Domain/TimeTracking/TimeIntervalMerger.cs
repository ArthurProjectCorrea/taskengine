namespace TaskEngine.Domain.TimeTracking;

/// <summary>
/// Merges possibly-overlapping <see cref="TimeInterval"/>s into the minimal set of disjoint
/// intervals covering the same total time, via the classic sweep-line algorithm (sort by start,
/// then fold each interval into the last one whenever it overlaps or touches it). Used to satisfy
/// RN-007 (ERS-Tarefas.md): when human and AI activity happen in the same period, that period
/// must count once toward the total time invested, even though it still counts separately toward
/// each origin's own total.
/// </summary>
public static class TimeIntervalMerger
{
    /// <summary>
    /// Returns the disjoint, time-ordered intervals that cover exactly the same time as
    /// <paramref name="intervals"/>, with any overlapping or touching (End == next Start)
    /// intervals folded into one.
    /// </summary>
    public static IReadOnlyList<TimeInterval> Merge(IEnumerable<TimeInterval> intervals)
    {
        List<TimeInterval> sorted = intervals.OrderBy(i => i.Start).ToList();
        if (sorted.Count == 0)
        {
            return sorted;
        }

        var merged = new List<TimeInterval>(sorted.Count) { sorted[0] };

        for (var i = 1; i < sorted.Count; i++)
        {
            TimeInterval current = sorted[i];
            TimeInterval last = merged[^1];

            if (current.Start <= last.End)
            {
                if (current.End > last.End)
                {
                    merged[^1] = new TimeInterval(last.Start, current.End);
                }
                // else current is fully contained within last - nothing to do.
            }
            else
            {
                merged.Add(current);
            }
        }

        return merged;
    }

    /// <summary>
    /// Total duration covered by <paramref name="intervals"/>, counting any overlap between them
    /// only once (RN-007).
    /// </summary>
    public static TimeSpan TotalDuration(IEnumerable<TimeInterval> intervals)
    {
        var total = TimeSpan.Zero;
        foreach (TimeInterval interval in Merge(intervals))
        {
            total += interval.Duration;
        }

        return total;
    }
}
