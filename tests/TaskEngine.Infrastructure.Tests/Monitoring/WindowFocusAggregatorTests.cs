using TaskEngine.Infrastructure.Monitoring;

namespace TaskEngine.Infrastructure.Tests.Monitoring;

public class WindowFocusAggregatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Observe_FirstWindow_ReturnsNull()
    {
        var aggregator = new WindowFocusAggregator();

        var completed = aggregator.Observe("chrome", "GitHub", Start);

        Assert.Null(completed);
    }

    [Fact]
    public void Observe_SameWindowRepeated_ReturnsNullAndExtendsInterval()
    {
        var aggregator = new WindowFocusAggregator();
        aggregator.Observe("chrome", "GitHub", Start);

        var completed = aggregator.Observe("chrome", "GitHub", Start.AddSeconds(2));

        Assert.Null(completed);
    }

    [Fact]
    public void Observe_WindowChanges_ReturnsCompletedIntervalForThePreviousWindow()
    {
        var aggregator = new WindowFocusAggregator();
        aggregator.Observe("chrome", "GitHub", Start);
        aggregator.Observe("chrome", "GitHub", Start.AddSeconds(3));

        var completed = aggregator.Observe("devenv", "Solution", Start.AddSeconds(4));

        Assert.NotNull(completed);
        Assert.Equal("chrome", completed!.Value.ProcessName);
        Assert.Equal("GitHub", completed.Value.WindowTitle);
        Assert.Equal(Start, completed.Value.StartedAt);
        Assert.Equal(Start.AddSeconds(3), completed.Value.EndedAt);
    }

    [Fact]
    public void Observe_SameProcessDifferentTitle_IsTreatedAsADifferentWindow()
    {
        var aggregator = new WindowFocusAggregator();
        aggregator.Observe("chrome", "GitHub", Start);
        aggregator.Observe("chrome", "GitHub", Start.AddSeconds(2));

        var completed = aggregator.Observe("chrome", "Stack Overflow", Start.AddSeconds(5));

        Assert.NotNull(completed);
        Assert.Equal("GitHub", completed!.Value.WindowTitle);
    }

    [Fact]
    public void Observe_SingleSampleBeforeSwitching_DoesNotProduceAZeroDurationInterval()
    {
        var aggregator = new WindowFocusAggregator();
        aggregator.Observe("chrome", "GitHub", Start);

        // No repeated observation of "chrome/GitHub" before switching away - only one sample ever
        // saw it, so there is no meaningful duration to report.
        var completed = aggregator.Observe("devenv", "Solution", Start.AddSeconds(1));

        Assert.Null(completed);
    }

    [Fact]
    public void Observe_NoForegroundWindow_ClosesThePreviousIntervalAndTracksNothing()
    {
        var aggregator = new WindowFocusAggregator();
        aggregator.Observe("chrome", "GitHub", Start);
        aggregator.Observe("chrome", "GitHub", Start.AddSeconds(2));

        var completed = aggregator.Observe(null, null, Start.AddSeconds(3));

        Assert.NotNull(completed);
        Assert.Equal("chrome", completed!.Value.ProcessName);

        var next = aggregator.Observe("chrome", "GitHub", Start.AddSeconds(4));
        Assert.Null(next);
    }

    [Fact]
    public void Flush_WithOngoingInterval_ReturnsItAndClearsState()
    {
        var aggregator = new WindowFocusAggregator();
        aggregator.Observe("chrome", "GitHub", Start);
        aggregator.Observe("chrome", "GitHub", Start.AddSeconds(2));

        var completed = aggregator.Flush(Start.AddSeconds(5));

        Assert.NotNull(completed);
        Assert.Equal(Start, completed!.Value.StartedAt);
        Assert.Equal(Start.AddSeconds(5), completed.Value.EndedAt);
    }

    [Fact]
    public void Flush_WithNoOngoingInterval_ReturnsNull()
    {
        var aggregator = new WindowFocusAggregator();

        Assert.Null(aggregator.Flush(Start));
    }

    [Fact]
    public void Flush_WithOnlyASingleSample_ReturnsNull()
    {
        var aggregator = new WindowFocusAggregator();
        aggregator.Observe("chrome", "GitHub", Start);

        Assert.Null(aggregator.Flush(Start.AddSeconds(1)));
    }
}
