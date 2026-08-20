using TaskEngine.Desktop.ViewModels.Reports;

namespace TaskEngine.Desktop.Tests;

public class TimelineLaneAllocatorTests
{
    private readonly record struct Interval(string Label, DateTimeOffset Start, DateTimeOffset End);

    private static DateTimeOffset At(int minutesFromEpoch) =>
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(minutesFromEpoch);

    private static IReadOnlyList<IReadOnlyList<Interval>> Allocate(params Interval[] items) =>
        TimelineLaneAllocator.Allocate((IReadOnlyList<Interval>)items, i => i.Start, i => i.End);

    [Fact]
    public void Allocate_NoItems_ReturnsNoLanes()
    {
        var lanes = Allocate();

        Assert.Empty(lanes);
    }

    [Fact]
    public void Allocate_SingleItem_ReturnsOneLaneWithOneItem()
    {
        var item = new Interval("a", At(0), At(10));

        var lanes = Allocate(item);

        var lane = Assert.Single(lanes);
        Assert.Equal([item], lane);
    }

    [Fact]
    public void Allocate_NonOverlappingItems_AllShareTheSameLane()
    {
        // Sequential, back-to-back items never overlap - a single lane suffices, same as the
        // approved prototype's own buildLanes behavior.
        var first = new Interval("a", At(0), At(10));
        var second = new Interval("b", At(10), At(20));
        var third = new Interval("c", At(25), At(30));

        var lanes = Allocate(first, second, third);

        var lane = Assert.Single(lanes);
        Assert.Equal([first, second, third], lane);
    }

    [Fact]
    public void Allocate_TwoOverlappingItems_GoToDifferentLanes()
    {
        // CA-010.1: overlapping periods between distinct items must stay visible - each gets its
        // own lane rather than being merged/dropped.
        var first = new Interval("a", At(0), At(30));
        var second = new Interval("b", At(15), At(45));

        var lanes = Allocate(first, second);

        Assert.Equal(2, lanes.Count);
        Assert.Equal([first], lanes[0]);
        Assert.Equal([second], lanes[1]);
    }

    [Fact]
    public void Allocate_ItemStartingExactlyWhenAnotherEnds_ReusesTheSameLane()
    {
        // Touching (not overlapping) intervals: laneEnd <= itemStart means the lane is free again.
        var first = new Interval("a", At(0), At(10));
        var second = new Interval("b", At(10), At(20));

        var lanes = Allocate(first, second);

        var lane = Assert.Single(lanes);
        Assert.Equal([first, second], lane);
    }

    [Fact]
    public void Allocate_ThreeMutuallyOverlappingItems_UseThreeSeparateLanes()
    {
        var first = new Interval("a", At(0), At(30));
        var second = new Interval("b", At(5), At(25));
        var third = new Interval("c", At(10), At(20));

        var lanes = Allocate(first, second, third);

        Assert.Equal(3, lanes.Count);
    }

    [Fact]
    public void Allocate_LaneFreedByAnEarlierItem_IsReusedByALaterNonOverlappingItem()
    {
        // a: 0-10 (lane 0), b: 0-20 (lane 1, overlaps a), c: 12-15 (fits back in lane 0, since a
        // already ended at 10) - greedy packing should reuse lane 0 instead of opening a third lane.
        var a = new Interval("a", At(0), At(10));
        var b = new Interval("b", At(0), At(20));
        var c = new Interval("c", At(12), At(15));

        var lanes = Allocate(a, b, c);

        Assert.Equal(2, lanes.Count);
        Assert.Equal([a, c], lanes[0]);
        Assert.Equal([b], lanes[1]);
    }

    [Fact]
    public void Allocate_ItemsOutOfChronologicalOrder_AreSortedByStartBeforeAllocation()
    {
        var later = new Interval("later", At(20), At(30));
        var earlier = new Interval("earlier", At(0), At(10));

        var lanes = Allocate(later, earlier);

        var lane = Assert.Single(lanes);
        Assert.Equal([earlier, later], lane);
    }
}
