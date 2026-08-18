using TaskEngine.Domain.Entities;

namespace TaskEngine.Domain.Tests;

public class ActivityIntervalTests
{
    [Fact]
    public void Duration_IsDifferenceBetweenStartAndEnd()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddMinutes(15);

        var interval = new ActivityInterval(ActivitySource.Human, start, end);

        Assert.Equal(TimeSpan.FromMinutes(15), interval.Duration);
    }

    [Fact]
    public void Constructor_ThrowsWhenEndedAtIsBeforeStartedAt()
    {
        var start = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => new ActivityInterval(ActivitySource.Ai, start, start.AddMinutes(-1)));
    }

    [Fact]
    public void Constructor_ThrowsWhenEndedAtEqualsStartedAt()
    {
        var start = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => new ActivityInterval(ActivitySource.Ai, start, start));
    }
}
