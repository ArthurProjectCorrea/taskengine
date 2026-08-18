using TaskEngine.Domain.Entities;
using TaskEngine.Infrastructure.Persistence;

namespace TaskEngine.Infrastructure.Tests.Persistence;

public class SqliteUnmappedTimeEntryRepositoryTests
{
    [Fact]
    public async Task AddAsync_ThenListByTaskIdAsync_RoundTripsEntry()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteUnmappedTimeEntryRepository(db.PathProvider);

        var taskId = Guid.NewGuid();
        var recordedAt = new DateTimeOffset(2026, 1, 5, 14, 30, 0, TimeSpan.Zero);
        var entry = UnmappedTimeEntry.Create(taskId, TimeSpan.FromMinutes(45), "Pair reviewed on paper.", recordedAt);

        await repository.AddAsync(entry, CancellationToken.None);
        var entries = await repository.ListByTaskIdAsync(taskId, CancellationToken.None);

        var loaded = Assert.Single(entries);
        Assert.Equal(entry.Id, loaded.Id);
        Assert.Equal(taskId, loaded.TaskId);
        Assert.Equal(TimeSpan.FromMinutes(45), loaded.Duration);
        Assert.Equal("Pair reviewed on paper.", loaded.Justification);
        Assert.Equal(recordedAt, loaded.RecordedAt);
    }

    [Fact]
    public async Task ListByTaskIdAsync_ReturnsOnlyEntriesForThatTask()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteUnmappedTimeEntryRepository(db.PathProvider);

        var taskId = Guid.NewGuid();
        var otherTaskId = Guid.NewGuid();
        var recordedAt = DateTimeOffset.UtcNow;

        await repository.AddAsync(
            UnmappedTimeEntry.Create(taskId, TimeSpan.FromMinutes(20), "Justification A", recordedAt),
            CancellationToken.None);
        await repository.AddAsync(
            UnmappedTimeEntry.Create(taskId, TimeSpan.FromMinutes(10), "Justification B", recordedAt),
            CancellationToken.None);
        await repository.AddAsync(
            UnmappedTimeEntry.Create(otherTaskId, TimeSpan.FromMinutes(5), "Justification C", recordedAt),
            CancellationToken.None);

        var entries = await repository.ListByTaskIdAsync(taskId, CancellationToken.None);

        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.Equal(taskId, e.TaskId));
    }

    [Fact]
    public async Task ListByTaskIdAsync_WhenNoEntries_ReturnsEmpty()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteUnmappedTimeEntryRepository(db.PathProvider);

        var entries = await repository.ListByTaskIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(entries);
    }
}
