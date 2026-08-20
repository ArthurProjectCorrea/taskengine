namespace TaskEngine.Desktop.ViewModels.Reports;

/// <summary>
/// Allocates timeline items into non-overlapping visual "lanes" for the per-task activity timeline
/// screen (plan item 9, RF-010/CA-010.1, ERS-Tarefas.md): a pure, MAUI-free port of the approved
/// prototype's own greedy lane-packing logic (`buildLanes` in
/// `docs/prototipo/TaskEngine Relatório.dc.html`) - items are sorted by start time, then each item
/// is placed into the first lane whose most-recently placed item already ended at or before this
/// item's start; a new lane is opened only when no such lane exists.
///
/// This intentionally does not merge or drop overlapping items (CA-010.1 requires distinct items'
/// periods to stay visible even when they overlap) - two items overlapping in time simply end up in
/// different lanes, so no lane ever shows two overlapping blocks.
///
/// Generic over the item type and takes <paramref name="start"/>/<paramref name="end"/> selectors
/// (rather than depending on <c>TaskActivityTimelineRow</c> directly) so it can be unit-tested with
/// plain data, independent of TaskEngine.Application.
/// </summary>
public static class TimelineLaneAllocator
{
    public static IReadOnlyList<IReadOnlyList<T>> Allocate<T>(
        IReadOnlyList<T> items,
        Func<T, DateTimeOffset> start,
        Func<T, DateTimeOffset> end)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);

        var lanes = new List<List<T>>();
        var laneEnds = new List<DateTimeOffset>();

        foreach (T item in items.OrderBy(start))
        {
            DateTimeOffset itemStart = start(item);
            var laneIndex = laneEnds.FindIndex(laneEnd => laneEnd <= itemStart);
            if (laneIndex == -1)
            {
                laneIndex = lanes.Count;
                lanes.Add([]);
                laneEnds.Add(default);
            }

            lanes[laneIndex].Add(item);
            laneEnds[laneIndex] = end(item);
        }

        return lanes;
    }
}
