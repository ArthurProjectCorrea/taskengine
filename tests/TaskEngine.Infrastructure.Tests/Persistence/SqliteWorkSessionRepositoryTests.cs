using TaskEngine.Domain.Entities;
using TaskEngine.Infrastructure.Persistence;

namespace TaskEngine.Infrastructure.Tests.Persistence;

public class SqliteWorkSessionRepositoryTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsOpenSessionWithActivities()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteWorkSessionRepository(db.PathProvider);

        var taskId = Guid.NewGuid();
        var session = WorkSession.Start(taskId, Start);
        session.RecordActivity(ActivitySource.Human, Start, Start.AddMinutes(10));
        session.RecordActivity(ActivitySource.Ai, Start.AddMinutes(10), Start.AddMinutes(15));

        await repository.AddAsync(session, CancellationToken.None);
        var loaded = await repository.GetByIdAsync(session.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(session.Id, loaded!.Id);
        Assert.Equal(taskId, loaded.TaskId);
        Assert.Equal(Start, loaded.StartedAt);
        Assert.Null(loaded.EndedAt);
        Assert.True(loaded.IsOpen);
        Assert.Equal(session.Activities, loaded.Activities);
    }

    [Fact]
    public async Task UpdateAsync_PersistsEndedAtAndActivitiesRecordedAfterAdd()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteWorkSessionRepository(db.PathProvider);

        var session = WorkSession.Start(Guid.NewGuid(), Start);
        session.RecordActivity(ActivitySource.Human, Start, Start.AddMinutes(10));
        await repository.AddAsync(session, CancellationToken.None);

        session.RecordActivity(ActivitySource.Ai, Start.AddMinutes(10), Start.AddMinutes(20));
        session.End(Start.AddMinutes(20));
        await repository.UpdateAsync(session, CancellationToken.None);

        var loaded = await repository.GetByIdAsync(session.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(Start.AddMinutes(20), loaded!.EndedAt);
        Assert.False(loaded.IsOpen);
        Assert.Equal(session.Activities, loaded.Activities);
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsActivityIdentityAndSelection()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteWorkSessionRepository(db.PathProvider);

        var session = WorkSession.Start(Guid.NewGuid(), Start);
        session.RecordActivity(
            ActivitySource.Human, Start, Start.AddMinutes(10), ActivityItemType.File, "C:/repo/file.cs");
        session.ApplyActivitySelection(new HashSet<Guid> { session.Activities[0].Id });

        await repository.AddAsync(session, CancellationToken.None);
        var loaded = await repository.GetByIdAsync(session.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        var activity = Assert.Single(loaded!.Activities);
        Assert.Equal(session.Activities[0].Id, activity.Id);
        Assert.Equal(ActivityItemType.File, activity.Type);
        Assert.Equal("C:/repo/file.cs", activity.Path);
        Assert.True(activity.SelectedAtConclusion);
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsTypeAndOrigin()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteWorkSessionRepository(db.PathProvider);

        var taskId = Guid.NewGuid();
        var session = WorkSession.Start(taskId, Start, WorkSessionType.Pause, WorkSessionOrigin.Provider);

        await repository.AddAsync(session, CancellationToken.None);
        var loaded = await repository.GetByIdAsync(session.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(WorkSessionType.Pause, loaded!.Type);
        Assert.Equal(WorkSessionOrigin.Provider, loaded.Origin);
    }

    [Fact]
    public async Task GetByIdAsync_WhenSessionDoesNotExist_ReturnsNull()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteWorkSessionRepository(db.PathProvider);

        var loaded = await repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task ListByTaskIdAsync_ReturnsOnlySessionsForThatTask()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var repository = new SqliteWorkSessionRepository(db.PathProvider);

        var taskId = Guid.NewGuid();
        var otherTaskId = Guid.NewGuid();
        var session1 = WorkSession.Start(taskId, Start);
        var session2 = WorkSession.Start(taskId, Start.AddHours(1));
        var otherSession = WorkSession.Start(otherTaskId, Start);

        await repository.AddAsync(session1, CancellationToken.None);
        await repository.AddAsync(session2, CancellationToken.None);
        await repository.AddAsync(otherSession, CancellationToken.None);

        var sessions = await repository.ListByTaskIdAsync(taskId, CancellationToken.None);

        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, s => Assert.Equal(taskId, s.TaskId));
    }
}
