using TaskEngine.Application.Reports;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Tests;

public class GenerateTaskReportUseCaseTests
{
    private static GenerateTaskReportUseCase CreateUseCase(
        FakeTaskRepository taskRepository,
        FakeWorkSessionRepository workSessionRepository,
        FakeWorkScheduleStore? workScheduleStore = null,
        FakeUnmappedTimeEntryRepository? unmappedTimeEntryRepository = null)
    {
        return new GenerateTaskReportUseCase(
            taskRepository,
            workSessionRepository,
            workScheduleStore ?? new FakeWorkScheduleStore(),
            unmappedTimeEntryRepository ?? new FakeUnmappedTimeEntryRepository());
    }

    [Fact]
    public async Task ExecuteAsync_TaskWithNoSessions_HasNullStartAndEndAndZeroDurations()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        await taskRepository.AddAsync(task, CancellationToken.None);
        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync();

        TaskReportRow row = Assert.Single(rows);
        Assert.Equal(task.Id, row.TaskId);
        Assert.Null(row.StartedAt);
        Assert.Null(row.EndedAt);
        Assert.Equal(0, row.AiSeconds);
        Assert.Equal(0, row.HumanSeconds);
        Assert.Equal(0, row.ScheduledSeconds);
        Assert.Equal(0, row.UnmappedSeconds);
        Assert.Empty(row.UnmappedJustifications);
        Assert.Equal(0, row.TotalSeconds);
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

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync();

        TaskReportRow row = Assert.Single(rows);
        Assert.Equal(DateOnly.FromDateTime(startedAt.UtcDateTime), row.StartedAt);
        Assert.Equal(DateOnly.FromDateTime(startedAt.AddMinutes(45).UtcDateTime), row.EndedAt);
        Assert.Equal(TimeSpan.FromMinutes(15).TotalSeconds, row.AiSeconds);
        Assert.Equal(TimeSpan.FromMinutes(30).TotalSeconds, row.HumanSeconds);
        Assert.Equal(TimeSpan.FromMinutes(45).TotalSeconds, row.TotalSeconds);
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

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

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

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

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

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

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

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync();

        TaskReportRow row = Assert.Single(rows);
        Assert.Equal("github", row.ProviderId);
    }

    [Fact]
    public async Task ExecuteAsync_OverlappingHumanAndAiActivity_TotalSecondsCountsOverlapOnce()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var startedAt = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var session = WorkSession.Start(task.Id, startedAt);
        // Human 0-30min, AI 15-45min: 30min of overlap between the two origins.
        session.RecordActivity(ActivitySource.Human, startedAt, startedAt.AddMinutes(30));
        session.RecordActivity(ActivitySource.Ai, startedAt.AddMinutes(15), startedAt.AddMinutes(45));
        session.End(startedAt.AddMinutes(45));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync();

        TaskReportRow row = Assert.Single(rows);
        Assert.Equal(TimeSpan.FromMinutes(30).TotalSeconds, row.HumanSeconds);
        Assert.Equal(TimeSpan.FromMinutes(30).TotalSeconds, row.AiSeconds);
        // Naively summing would give 60min; merged, the 15-30min overlap only counts once.
        Assert.Equal(TimeSpan.FromMinutes(45).TotalSeconds, row.TotalSeconds);
    }

    [Fact]
    public async Task ExecuteAsync_OverlappingActivitiesWithinSameOrigin_HumanSecondsCountsOverlapOnce()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var startedAt = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var session = WorkSession.Start(task.Id, startedAt);
        // Two files edited by the human in an overlapping window: 0-20min and 10-30min.
        session.RecordActivity(ActivitySource.Human, startedAt, startedAt.AddMinutes(20));
        session.RecordActivity(ActivitySource.Human, startedAt.AddMinutes(10), startedAt.AddMinutes(30));
        session.End(startedAt.AddMinutes(30));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync();

        TaskReportRow row = Assert.Single(rows);
        Assert.Equal(TimeSpan.FromMinutes(30).TotalSeconds, row.HumanSeconds);
    }

    [Fact]
    public async Task ExecuteAsync_PausedSessionActivity_IsExcludedFromDurations()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var startedAt = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var activeSession = WorkSession.Start(task.Id, startedAt, WorkSessionType.Active);
        activeSession.RecordActivity(ActivitySource.Human, startedAt, startedAt.AddMinutes(10));
        activeSession.End(startedAt.AddMinutes(10));
        await workSessionRepository.AddAsync(activeSession, CancellationToken.None);

        var pauseSession = WorkSession.Start(task.Id, startedAt.AddMinutes(10), WorkSessionType.Pause);
        pauseSession.End(startedAt.AddHours(2));
        await workSessionRepository.AddAsync(pauseSession, CancellationToken.None);

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync();

        TaskReportRow row = Assert.Single(rows);
        Assert.Equal(TimeSpan.FromMinutes(10).TotalSeconds, row.HumanSeconds);
        Assert.Equal(TimeSpan.FromMinutes(10).TotalSeconds, row.TotalSeconds);
        // The pause session's own end still counts toward the task's overall EndedAt.
        Assert.Equal(DateOnly.FromDateTime(startedAt.AddHours(2).UtcDateTime), row.EndedAt);
    }

    [Fact]
    public async Task ExecuteAsync_UnmappedTimeEntries_SumsDurationsAndCollectsJustifications()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var unmappedTimeEntryRepository = new FakeUnmappedTimeEntryRepository();
        var task = TaskItem.Create("Write report");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var recordedAt = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        await unmappedTimeEntryRepository.AddAsync(
            UnmappedTimeEntry.Create(task.Id, TimeSpan.FromMinutes(20), "Reunião presencial", recordedAt),
            CancellationToken.None);
        await unmappedTimeEntryRepository.AddAsync(
            UnmappedTimeEntry.Create(task.Id, TimeSpan.FromMinutes(10), "Ligação com o cliente", recordedAt),
            CancellationToken.None);

        var useCase = CreateUseCase(
            taskRepository, workSessionRepository, unmappedTimeEntryRepository: unmappedTimeEntryRepository);

        var rows = await useCase.ExecuteAsync();

        TaskReportRow row = Assert.Single(rows);
        Assert.Equal(TimeSpan.FromMinutes(30).TotalSeconds, row.UnmappedSeconds);
        Assert.Equal(["Reunião presencial", "Ligação com o cliente"], row.UnmappedJustifications);
    }

    [Fact]
    public async Task ExecuteAsync_WithSchedule_ComputesScheduledSecondsOverTaskPeriod()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var workScheduleStore = new FakeWorkScheduleStore();
        var schedule = WorkSchedule.Create(
            new TimeOnly(9, 0), new TimeOnly(18, 0), new TimeOnly(12, 0), new TimeOnly(13, 0),
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);
        await workScheduleStore.SaveAsync(schedule, CancellationToken.None);

        var task = TaskItem.Create("Write report");
        await taskRepository.AddAsync(task, CancellationToken.None);

        // Monday 2026-01-12 09:00 through Tuesday 2026-01-13 (both work days) -> 8h expedient/day.
        var startedAt = new DateTimeOffset(2026, 1, 12, 9, 0, 0, TimeSpan.Zero);
        var session = WorkSession.Start(task.Id, startedAt);
        session.End(new DateTimeOffset(2026, 1, 13, 17, 0, 0, TimeSpan.Zero));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var useCase = CreateUseCase(taskRepository, workSessionRepository, workScheduleStore);

        var rows = await useCase.ExecuteAsync();

        TaskReportRow row = Assert.Single(rows);
        Assert.Equal(TimeSpan.FromHours(16).TotalSeconds, row.ScheduledSeconds);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutSchedule_ScheduledSecondsIsZero()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Write report");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var startedAt = new DateTimeOffset(2026, 1, 12, 9, 0, 0, TimeSpan.Zero);
        var session = WorkSession.Start(task.Id, startedAt);
        session.End(startedAt.AddHours(1));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync();

        TaskReportRow row = Assert.Single(rows);
        Assert.Equal(0, row.ScheduledSeconds);
    }

    [Fact]
    public async Task ExecuteAsync_NoPeriodFilter_ReturnsAllTasksIncludingThoseWithoutSessions()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var withSessions = TaskItem.Create("With sessions");
        var withoutSessions = TaskItem.Create("Without sessions");
        await taskRepository.AddAsync(withSessions, CancellationToken.None);
        await taskRepository.AddAsync(withoutSessions, CancellationToken.None);

        var session = WorkSession.Start(withSessions.Id, new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero));
        session.End(session.StartedAt.AddHours(1));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync();

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task ExecuteAsync_PeriodFilter_ExcludesTaskWithNoSessions()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Without sessions");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Empty(rows);
    }

    [Fact]
    public async Task ExecuteAsync_PeriodFilter_ExcludesTaskEntirelyBeforePeriod()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Old task");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var session = WorkSession.Start(task.Id, new DateTimeOffset(2025, 12, 1, 9, 0, 0, TimeSpan.Zero));
        session.End(session.StartedAt.AddHours(1));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Empty(rows);
    }

    [Fact]
    public async Task ExecuteAsync_PeriodFilter_ExcludesTaskEntirelyAfterPeriod()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Future task");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var session = WorkSession.Start(task.Id, new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
        session.End(session.StartedAt.AddHours(1));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Empty(rows);
    }

    [Fact]
    public async Task ExecuteAsync_PeriodFilter_IncludesTaskFullyWithinPeriod()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("In range task");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var session = WorkSession.Start(task.Id, new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero));
        session.End(new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Single(rows);
    }

    [Fact]
    public async Task ExecuteAsync_PeriodFilter_IncludesTaskOverlappingPeriodBoundary()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Straddling task");
        await taskRepository.AddAsync(task, CancellationToken.None);

        // Starts before the filtered period and ends inside it.
        var session = WorkSession.Start(task.Id, new DateTimeOffset(2025, 12, 20, 9, 0, 0, TimeSpan.Zero));
        session.End(new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Single(rows);
    }

    [Fact]
    public async Task ExecuteAsync_PeriodFilter_IncludesStillOpenTaskStartedBeforePeriod()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Still going task");
        await taskRepository.AddAsync(task, CancellationToken.None);

        // Open session started before the filtered period, never closed - treated as ongoing.
        var session = WorkSession.Start(task.Id, new DateTimeOffset(2025, 12, 1, 9, 0, 0, TimeSpan.Zero));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Single(rows);
    }

    [Fact]
    public async Task ExecuteAsync_OnlyPeriodStartGiven_ExcludesTasksEndingBeforeIt()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Old task");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var session = WorkSession.Start(task.Id, new DateTimeOffset(2025, 1, 1, 9, 0, 0, TimeSpan.Zero));
        session.End(session.StartedAt.AddHours(1));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync(periodStart: new DateOnly(2026, 1, 1));

        Assert.Empty(rows);
    }

    [Fact]
    public async Task ExecuteAsync_OnlyPeriodEndGiven_ExcludesTasksStartingAfterIt()
    {
        var taskRepository = new FakeTaskRepository();
        var workSessionRepository = new FakeWorkSessionRepository();
        var task = TaskItem.Create("Future task");
        await taskRepository.AddAsync(task, CancellationToken.None);

        var session = WorkSession.Start(task.Id, new DateTimeOffset(2027, 1, 1, 9, 0, 0, TimeSpan.Zero));
        session.End(session.StartedAt.AddHours(1));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var useCase = CreateUseCase(taskRepository, workSessionRepository);

        var rows = await useCase.ExecuteAsync(periodEnd: new DateOnly(2026, 1, 1));

        Assert.Empty(rows);
    }
}
