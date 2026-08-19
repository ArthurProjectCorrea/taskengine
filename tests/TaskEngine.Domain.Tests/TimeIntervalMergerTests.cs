using TaskEngine.Domain.TimeTracking;

namespace TaskEngine.Domain.Tests;

public class TimeIntervalMergerTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Merge_WithNoIntervals_ReturnsEmpty()
    {
        IReadOnlyList<TimeInterval> merged = TimeIntervalMerger.Merge([]);

        Assert.Empty(merged);
    }

    [Fact]
    public void Merge_WithDisjointIntervals_ReturnsBothUnchanged()
    {
        var first = new TimeInterval(Start, Start.AddMinutes(10));
        var second = new TimeInterval(Start.AddMinutes(20), Start.AddMinutes(30));

        IReadOnlyList<TimeInterval> merged = TimeIntervalMerger.Merge([second, first]);

        Assert.Equal(2, merged.Count);
        Assert.Equal(first, merged[0]);
        Assert.Equal(second, merged[1]);
    }

    [Fact]
    public void Merge_WithPartialOverlap_FoldsIntoOneInterval()
    {
        // Human 0-10, AI 5-15 -> merged 0-15.
        var human = new TimeInterval(Start, Start.AddMinutes(10));
        var ai = new TimeInterval(Start.AddMinutes(5), Start.AddMinutes(15));

        IReadOnlyList<TimeInterval> merged = TimeIntervalMerger.Merge([human, ai]);

        var only = Assert.Single(merged);
        Assert.Equal(Start, only.Start);
        Assert.Equal(Start.AddMinutes(15), only.End);
    }

    [Fact]
    public void Merge_WithFullOverlap_KeepsTheWiderInterval()
    {
        // AI 0-30 fully contains human 10-20.
        var wide = new TimeInterval(Start, Start.AddMinutes(30));
        var contained = new TimeInterval(Start.AddMinutes(10), Start.AddMinutes(20));

        IReadOnlyList<TimeInterval> merged = TimeIntervalMerger.Merge([contained, wide]);

        var only = Assert.Single(merged);
        Assert.Equal(wide, only);
    }

    [Fact]
    public void Merge_WithTouchingIntervals_FoldsIntoOne()
    {
        var first = new TimeInterval(Start, Start.AddMinutes(10));
        var second = new TimeInterval(Start.AddMinutes(10), Start.AddMinutes(20));

        IReadOnlyList<TimeInterval> merged = TimeIntervalMerger.Merge([first, second]);

        var only = Assert.Single(merged);
        Assert.Equal(Start, only.Start);
        Assert.Equal(Start.AddMinutes(20), only.End);
    }

    [Fact]
    public void TotalDuration_WithDisjointIntervals_SumsThem()
    {
        var first = new TimeInterval(Start, Start.AddMinutes(10));
        var second = new TimeInterval(Start.AddMinutes(20), Start.AddMinutes(30));

        TimeSpan total = TimeIntervalMerger.TotalDuration([first, second]);

        Assert.Equal(TimeSpan.FromMinutes(20), total);
    }

    [Fact]
    public void TotalDuration_WithOverlappingIntervals_DoesNotDoubleCountTheOverlap()
    {
        // Human 0-10, AI 5-15 -> 15 minutes total, not 20 (RN-007).
        var human = new TimeInterval(Start, Start.AddMinutes(10));
        var ai = new TimeInterval(Start.AddMinutes(5), Start.AddMinutes(15));

        TimeSpan total = TimeIntervalMerger.TotalDuration([human, ai]);

        Assert.Equal(TimeSpan.FromMinutes(15), total);
    }

    [Fact]
    public void Constructor_ThrowsWhenEndIsNotAfterStart()
    {
        Assert.Throws<ArgumentException>(() => new TimeInterval(Start, Start));
        Assert.Throws<ArgumentException>(() => new TimeInterval(Start, Start.AddMinutes(-1)));
    }
}
