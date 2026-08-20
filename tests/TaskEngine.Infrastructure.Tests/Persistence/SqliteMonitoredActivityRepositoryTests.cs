using TaskEngine.Domain.Entities;
using TaskEngine.Infrastructure.Persistence;

namespace TaskEngine.Infrastructure.Tests.Persistence;

public class SqliteMonitoredActivityRepositoryTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddAsync_ThenListByPeriodAsync_RoundTripsTheActivity()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteMonitoredActivityRepository(db.PathProvider);

        var activity = new ActivityInterval(
            ActivitySource.Ai, Start, Start.AddMinutes(10), ActivityItemType.File, "C:/repo/file.cs");
        await repository.AddAsync(activity, CancellationToken.None);

        var results = await repository.ListByPeriodAsync(Start, Start.AddMinutes(10), CancellationToken.None);

        var loaded = Assert.Single(results);
        Assert.Equal(activity.Id, loaded.Id);
        Assert.Equal(ActivitySource.Ai, loaded.Source);
        Assert.Equal(Start, loaded.StartedAt);
        Assert.Equal(Start.AddMinutes(10), loaded.EndedAt);
        Assert.Equal(ActivityItemType.File, loaded.Type);
        Assert.Equal("C:/repo/file.cs", loaded.Path);
    }

    [Fact]
    public async Task ListByPeriodAsync_ReturnsActivitiesThatOverlapThePeriod()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteMonitoredActivityRepository(db.PathProvider);

        var before = new ActivityInterval(ActivitySource.Human, Start.AddHours(-2), Start.AddHours(-1));
        var overlapping = new ActivityInterval(ActivitySource.Human, Start.AddMinutes(-5), Start.AddMinutes(5));
        var inside = new ActivityInterval(ActivitySource.Ai, Start.AddMinutes(1), Start.AddMinutes(2));
        var after = new ActivityInterval(ActivitySource.Human, Start.AddHours(2), Start.AddHours(3));

        foreach (var activity in new[] { before, overlapping, inside, after })
        {
            await repository.AddAsync(activity, CancellationToken.None);
        }

        var results = await repository.ListByPeriodAsync(Start, Start.AddMinutes(10), CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, a => a.Id == overlapping.Id);
        Assert.Contains(results, a => a.Id == inside.Id);
    }

    [Fact]
    public async Task ListByPeriodAsync_WhenActivityPredatesTheQueriedTask_IsStillReturned()
    {
        // RF-004/CA-004.1: activity recorded before a task existed must still be recoverable once
        // the task is registered with a period that covers it.
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteMonitoredActivityRepository(db.PathProvider);

        var oldActivity = new ActivityInterval(ActivitySource.Human, Start.AddDays(-30), Start.AddDays(-30).AddMinutes(20));
        await repository.AddAsync(oldActivity, CancellationToken.None);

        var results = await repository.ListByPeriodAsync(
            Start.AddDays(-30), Start.AddDays(-30).AddMinutes(30), CancellationToken.None);

        var loaded = Assert.Single(results);
        Assert.Equal(oldActivity.Id, loaded.Id);
    }

    [Fact]
    public async Task ListByPeriodAsync_WhenNothingMatches_ReturnsEmpty()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteMonitoredActivityRepository(db.PathProvider);

        var results = await repository.ListByPeriodAsync(Start, Start.AddMinutes(10), CancellationToken.None);

        Assert.Empty(results);
    }
}
