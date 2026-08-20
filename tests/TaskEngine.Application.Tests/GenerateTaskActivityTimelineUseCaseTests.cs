using TaskEngine.Application.Reports;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Tests;

public class GenerateTaskActivityTimelineUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_UnknownTask_Throws()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var useCase = new GenerateTaskActivityTimelineUseCase(taskRepository, workSessionRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData("ToDo")]
    [InlineData("InProgress")]
    [InlineData("Paused")]
    public async Task ExecuteAsync_TaskNotCompleted_Throws(string statusName)
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        if (statusName != "ToDo")
        {
            task.Start();
        }

        if (statusName == "Paused")
        {
            task.Pause();
        }

        await taskRepository.AddAsync(task, CancellationToken.None);
        var useCase = new GenerateTaskActivityTimelineUseCase(taskRepository, workSessionRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(task.Id));
    }

    [Fact]
    public async Task ExecuteAsync_DonePendingSyncTask_IsAllowed()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        task.Start();
        task.CompleteOffline();
        await taskRepository.AddAsync(task, CancellationToken.None);
        var useCase = new GenerateTaskActivityTimelineUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync(task.Id);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task ExecuteAsync_OnlySelectedActivitiesAreIncluded()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var startedAt = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var session = WorkSession.Start(task.Id, startedAt);
        session.RecordActivity(
            ActivitySource.Human, startedAt, startedAt.AddMinutes(10), ActivityItemType.File, "src/a.cs");
        session.RecordActivity(
            ActivitySource.Ai, startedAt.AddMinutes(10), startedAt.AddMinutes(20), ActivityItemType.File, "src/b.cs");
        session.ApplyActivitySelection(new HashSet<Guid> { session.Activities[0].Id });
        session.End(startedAt.AddMinutes(20));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        task.Complete();
        await taskRepository.UpdateAsync(task, CancellationToken.None);

        var useCase = new GenerateTaskActivityTimelineUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync(task.Id);

        TaskActivityTimelineRow row = Assert.Single(rows);
        Assert.Equal("src/a.cs", row.Path);
        Assert.Equal(ActivityItemType.File, row.Type);
        Assert.Equal(ActivitySource.Human, row.Source);
        Assert.Equal(startedAt, row.StartedAt);
        Assert.Equal(TimeSpan.FromMinutes(10), row.Duration);
    }

    [Fact]
    public async Task ExecuteAsync_OverlappingSelectedItems_PreservesBothWithoutMerging()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var startedAt = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var session = WorkSession.Start(task.Id, startedAt);
        // Two distinct files, overlapping in time - both selected at conclusion.
        session.RecordActivity(
            ActivitySource.Human, startedAt, startedAt.AddMinutes(30), ActivityItemType.File, "src/a.cs");
        session.RecordActivity(
            ActivitySource.Human, startedAt.AddMinutes(15), startedAt.AddMinutes(45), ActivityItemType.File, "src/b.cs");
        session.ApplyActivitySelection(
            new HashSet<Guid> { session.Activities[0].Id, session.Activities[1].Id });
        session.End(startedAt.AddMinutes(45));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        task.Complete();
        await taskRepository.UpdateAsync(task, CancellationToken.None);

        var useCase = new GenerateTaskActivityTimelineUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync(task.Id);

        Assert.Equal(2, rows.Count);
        Assert.Equal("src/a.cs", rows[0].Path);
        Assert.Equal(TimeSpan.FromMinutes(30), rows[0].Duration);
        Assert.Equal("src/b.cs", rows[1].Path);
        Assert.Equal(TimeSpan.FromMinutes(30), rows[1].Duration);
    }

    [Fact]
    public async Task ExecuteAsync_ActivitiesAcrossMultipleSessions_AreOrderedByStartTime()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        task.Start();
        await taskRepository.AddAsync(task, CancellationToken.None);

        var firstStart = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var firstSession = WorkSession.Start(task.Id, firstStart);
        firstSession.RecordActivity(
            ActivitySource.Human, firstStart, firstStart.AddMinutes(10), ActivityItemType.Browser, "https://b.com");
        firstSession.ApplyActivitySelection(new HashSet<Guid> { firstSession.Activities[0].Id });
        firstSession.End(firstStart.AddMinutes(10));
        await workSessionRepository.AddAsync(firstSession, CancellationToken.None);

        var secondStart = firstStart.AddHours(3);
        var secondSession = WorkSession.Start(task.Id, secondStart);
        secondSession.RecordActivity(
            ActivitySource.Human, secondStart, secondStart.AddMinutes(5), ActivityItemType.File, "src/c.cs");
        secondSession.ApplyActivitySelection(new HashSet<Guid> { secondSession.Activities[0].Id });
        secondSession.End(secondStart.AddMinutes(5));
        await workSessionRepository.AddAsync(secondSession, CancellationToken.None);

        task.Complete();
        await taskRepository.UpdateAsync(task, CancellationToken.None);

        var useCase = new GenerateTaskActivityTimelineUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync(task.Id);

        Assert.Equal(2, rows.Count);
        Assert.Equal("https://b.com", rows[0].Path);
        Assert.Equal("src/c.cs", rows[1].Path);
    }
}
