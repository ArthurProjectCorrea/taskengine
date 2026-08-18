using TaskEngine.Application.Reports;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Tests;

public class GenerateTaskReportUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_TaskWithNoSessions_HasNullStartAndEndAndZeroDurations()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        await taskRepository.AddAsync(task, CancellationToken.None);
        var useCase = new GenerateTaskReportUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync();

        TaskReportRow row = Assert.Single(rows);
        Assert.Equal(task.Id, row.TaskId);
        Assert.Null(row.StartedAt);
        Assert.Null(row.EndedAt);
        Assert.Equal(0, row.AiSeconds);
        Assert.Equal(0, row.HumanSeconds);
    }

    [Fact]
    public async Task ExecuteAsync_TaskWithOneClosedSession_ComputesStartEndAndDurations()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var startedAt = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var session = WorkSession.Start(task.Id, startedAt);
        session.RecordActivity(ActivitySource.Human, startedAt, startedAt.AddMinutes(30));
        session.RecordActivity(ActivitySource.Ai, startedAt.AddMinutes(30), startedAt.AddMinutes(45));
        session.End(startedAt.AddMinutes(45));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var useCase = new GenerateTaskReportUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync();

        TaskReportRow row = Assert.Single(rows);
        Assert.Equal(DateOnly.FromDateTime(startedAt.UtcDateTime), row.StartedAt);
        Assert.Equal(DateOnly.FromDateTime(startedAt.AddMinutes(45).UtcDateTime), row.EndedAt);
        Assert.Equal(TimeSpan.FromMinutes(15).TotalSeconds, row.AiSeconds);
        Assert.Equal(TimeSpan.FromMinutes(30).TotalSeconds, row.HumanSeconds);
    }

    [Fact]
    public async Task ExecuteAsync_TaskWithMultipleSessions_StartIsEarliestAndEndIsLatestClosed()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var earlyStart = new DateTimeOffset(2026, 1, 5, 8, 0, 0, TimeSpan.Zero);
        var earlySession = WorkSession.Start(task.Id, earlyStart);
        earlySession.End(earlyStart.AddHours(1));
        await workSessionRepository.AddAsync(earlySession, CancellationToken.None);

        var lateStart = new DateTimeOffset(2026, 1, 20, 8, 0, 0, TimeSpan.Zero);
        var lateSession = WorkSession.Start(task.Id, lateStart);
        lateSession.End(lateStart.AddHours(2));
        await workSessionRepository.AddAsync(lateSession, CancellationToken.None);

        var useCase = new GenerateTaskReportUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync();

        TaskReportRow row = Assert.Single(rows);
        Assert.Equal(DateOnly.FromDateTime(earlyStart.UtcDateTime), row.StartedAt);
        Assert.Equal(DateOnly.FromDateTime(lateSession.EndedAt!.Value.UtcDateTime), row.EndedAt);
    }

    [Fact]
    public async Task ExecuteAsync_TaskWithOnlyOpenSession_HasNullEndDespiteHavingASession()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var startedAt = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var openSession = WorkSession.Start(task.Id, startedAt);
        await workSessionRepository.AddAsync(openSession, CancellationToken.None);

        var useCase = new GenerateTaskReportUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync();

        TaskReportRow row = Assert.Single(rows);
        Assert.Equal(DateOnly.FromDateTime(startedAt.UtcDateTime), row.StartedAt);
        Assert.Null(row.EndedAt);
    }

    [Fact]
    public async Task ExecuteAsync_TaskWithoutProviderId_HasNullProviderIdInRow()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var useCase = new GenerateTaskReportUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync();

        TaskReportRow row = Assert.Single(rows);
        Assert.Null(row.ProviderId);
    }

    [Fact]
    public async Task ExecuteAsync_TaskWithProviderId_CopiesItToRow()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        task.AttachProviderReference("github", "gh-1");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var useCase = new GenerateTaskReportUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync();

        TaskReportRow row = Assert.Single(rows);
        Assert.Equal("github", row.ProviderId);
    }
}
