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

    [Fact]
    public void Constructor_WithoutExplicitId_GeneratesAUniqueId()
    {
        var start = DateTimeOffset.UtcNow;

        var first = new ActivityInterval(ActivitySource.Human, start, start.AddMinutes(1));
        var second = new ActivityInterval(ActivitySource.Human, start, start.AddMinutes(1));

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Constructor_DefaultsTypePathAndSelectedAtConclusion()
    {
        var start = DateTimeOffset.UtcNow;

        var interval = new ActivityInterval(ActivitySource.Human, start, start.AddMinutes(1));

        Assert.Null(interval.Type);
        Assert.Null(interval.Path);
        Assert.False(interval.SelectedAtConclusion);
    }

    [Fact]
    public void Constructor_SetsTypeAndPathWhenProvided()
    {
        var start = DateTimeOffset.UtcNow;

        var interval = new ActivityInterval(
            ActivitySource.Ai, start, start.AddMinutes(1), ActivityItemType.File, "C:/repo/file.cs");

        Assert.Equal(ActivityItemType.File, interval.Type);
        Assert.Equal("C:/repo/file.cs", interval.Path);
    }
}
