using TaskEngine.Infrastructure.Monitoring;

namespace TaskEngine.Infrastructure.Tests.Monitoring;

public class FileActivityAggregatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(5);

    [Fact]
    public void Flush_BeforeDebounceWindowElapses_ReturnsNothing()
    {
        var aggregator = new FileActivityAggregator(DebounceWindow);
        aggregator.Record("C:/repo/file.cs", Start);

        var completed = aggregator.Flush(Start.Add(DebounceWindow).AddSeconds(-1));

        Assert.Empty(completed);
    }

    [Fact]
    public void Flush_AfterDebounceWindowElapses_ReturnsCompletedInterval()
    {
        var aggregator = new FileActivityAggregator(DebounceWindow);
        aggregator.Record("C:/repo/file.cs", Start);

        var completed = aggregator.Flush(Start.Add(DebounceWindow));

        var activity = Assert.Single(completed);
        Assert.Equal("C:/repo/file.cs", activity.Path);
        Assert.Equal(Start, activity.StartedAt);
        Assert.True(activity.EndedAt > activity.StartedAt);
    }

    [Fact]
    public void Record_MultipleChangesToSameFile_ExtendsThePendingIntervalInsteadOfSplitting()
    {
        var aggregator = new FileActivityAggregator(DebounceWindow);
        aggregator.Record("C:/repo/file.cs", Start);
        aggregator.Record("C:/repo/file.cs", Start.AddSeconds(1));
        aggregator.Record("C:/repo/file.cs", Start.AddSeconds(2));

        var completed = aggregator.Flush(Start.AddSeconds(2).Add(DebounceWindow));

        var activity = Assert.Single(completed);
        Assert.Equal(Start, activity.StartedAt);
        Assert.Equal(Start.AddSeconds(2), activity.EndedAt);
    }

    [Fact]
    public void Flush_DoesNotReturnTheSameIntervalTwice()
    {
        var aggregator = new FileActivityAggregator(DebounceWindow);
        aggregator.Record("C:/repo/file.cs", Start);

        var first = aggregator.Flush(Start.Add(DebounceWindow));
        var second = aggregator.Flush(Start.Add(DebounceWindow).AddMinutes(1));

        Assert.Single(first);
        Assert.Empty(second);
    }

    [Fact]
    public void Record_AfterFlush_OpensANewInterval()
    {
        var aggregator = new FileActivityAggregator(DebounceWindow);
        aggregator.Record("C:/repo/file.cs", Start);
        aggregator.Flush(Start.Add(DebounceWindow));

        var laterChange = Start.AddHours(1);
        aggregator.Record("C:/repo/file.cs", laterChange);
        var completed = aggregator.Flush(laterChange.Add(DebounceWindow));

        var activity = Assert.Single(completed);
        Assert.Equal(laterChange, activity.StartedAt);
    }

    [Fact]
    public void Record_DifferentFiles_TrackedIndependently()
    {
        var aggregator = new FileActivityAggregator(DebounceWindow);
        aggregator.Record("C:/repo/a.cs", Start);
        aggregator.Record("C:/repo/b.cs", Start.AddSeconds(1));

        var completed = aggregator.Flush(Start.AddSeconds(1).Add(DebounceWindow));

        Assert.Equal(2, completed.Count);
        Assert.Contains(completed, a => a.Path == "C:/repo/a.cs");
        Assert.Contains(completed, a => a.Path == "C:/repo/b.cs");
    }

    [Fact]
    public void Flush_StillPendingFiles_AreNotRemoved()
    {
        var aggregator = new FileActivityAggregator(DebounceWindow);
        aggregator.Record("C:/repo/quiet.cs", Start);
        aggregator.Record("C:/repo/busy.cs", Start.AddSeconds(1));

        // busy.cs keeps changing, so at this "now" only quiet.cs has been idle long enough.
        var now = Start.Add(DebounceWindow);
        var completed = aggregator.Flush(now);

        Assert.Single(completed);
        Assert.Equal("C:/repo/quiet.cs", completed[0].Path);
    }

    [Fact]
    public void Constructor_ThrowsForNonPositiveDebounceWindow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileActivityAggregator(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileActivityAggregator(TimeSpan.FromSeconds(-1)));
    }
}
