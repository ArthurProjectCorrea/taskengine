using TaskEngine.Domain.Entities;

namespace TaskEngine.Domain.Tests;

public class UnmappedTimeEntryTests
{
    private static readonly DateTimeOffset RecordedAt = new(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidData_CreatesEntry()
    {
        var taskId = Guid.NewGuid();
        var duration = TimeSpan.FromMinutes(30);

        var entry = UnmappedTimeEntry.Create(taskId, duration, "Discussed requirements offline.", RecordedAt);

        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal(taskId, entry.TaskId);
        Assert.Equal(duration, entry.Duration);
        Assert.Equal("Discussed requirements offline.", entry.Justification);
        Assert.Equal(RecordedAt, entry.RecordedAt);
    }

    [Fact]
    public void Create_ThrowsWhenDurationIsZero()
    {
        Assert.Throws<ArgumentException>(() => UnmappedTimeEntry.Create(
            Guid.NewGuid(), TimeSpan.Zero, "Justification", RecordedAt));
    }

    [Fact]
    public void Create_ThrowsWhenDurationIsNegative()
    {
        Assert.Throws<ArgumentException>(() => UnmappedTimeEntry.Create(
            Guid.NewGuid(), TimeSpan.FromMinutes(-1), "Justification", RecordedAt));
    }

    [Fact]
    public void Create_ThrowsWhenJustificationIsNull()
    {
        Assert.Throws<ArgumentException>(() => UnmappedTimeEntry.Create(
            Guid.NewGuid(), TimeSpan.FromMinutes(30), null!, RecordedAt));
    }

    [Fact]
    public void Create_ThrowsWhenJustificationIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => UnmappedTimeEntry.Create(
            Guid.NewGuid(), TimeSpan.FromMinutes(30), string.Empty, RecordedAt));
    }

    [Fact]
    public void Create_ThrowsWhenJustificationIsWhitespace()
    {
        Assert.Throws<ArgumentException>(() => UnmappedTimeEntry.Create(
            Guid.NewGuid(), TimeSpan.FromMinutes(30), "   ", RecordedAt));
    }
}
