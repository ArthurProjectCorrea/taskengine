using TaskEngine.Application.UnmappedTime;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Tests;

public class CalculateUnmappedTimeUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithNoSessions_ReturnsZero()
    {
        var workSessionRepository = new FakeWorkSessionRepository();
        var workScheduleStore = new FakeWorkScheduleStore();
        var unmappedTimeEntryRepository = new FakeUnmappedTimeEntryRepository();
        var useCase = new CalculateUnmappedTimeUseCase(workSessionRepository, workScheduleStore, unmappedTimeEntryRepository);

        TimeSpan? result = await useCase.ExecuteAsync(Guid.NewGuid());

        Assert.Equal(TimeSpan.Zero, result);
    }

    [Fact]
    public async Task ExecuteAsync_WithSessionsButNoScheduleConfigured_ReturnsNull()
    {
        var taskId = Guid.NewGuid();
        var workSessionRepository = new FakeWorkSessionRepository();
        var startedAt = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);
        var session = WorkSession.Start(taskId, startedAt);
        session.End(startedAt.AddHours(1));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var workScheduleStore = new FakeWorkScheduleStore();
        var unmappedTimeEntryRepository = new FakeUnmappedTimeEntryRepository();
        var useCase = new CalculateUnmappedTimeUseCase(workSessionRepository, workScheduleStore, unmappedTimeEntryRepository);

        TimeSpan? result = await useCase.ExecuteAsync(taskId);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoClosedSession_ReturnsZero()
    {
        var taskId = Guid.NewGuid();
        var workSessionRepository = new FakeWorkSessionRepository();
        var startedAt = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);
        var openSession = WorkSession.Start(taskId, startedAt);
        await workSessionRepository.AddAsync(openSession, CancellationToken.None);

        var workScheduleStore = new FakeWorkScheduleStore();
        await workScheduleStore.SaveAsync(
            WorkSchedule.Create(new TimeOnly(9, 0), new TimeOnly(18, 0), new TimeOnly(12, 0), new TimeOnly(13, 0),
                [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]),
            CancellationToken.None);

        var unmappedTimeEntryRepository = new FakeUnmappedTimeEntryRepository();
        var useCase = new CalculateUnmappedTimeUseCase(workSessionRepository, workScheduleStore, unmappedTimeEntryRepository);

        TimeSpan? result = await useCase.ExecuteAsync(taskId);

        Assert.Equal(TimeSpan.Zero, result);
    }

    [Fact]
    public async Task ExecuteAsync_WhenScheduledExceedsMapped_ReturnsDifference()
    {
        // Monday 2026-01-05, 9-18 with 12-13 lunch = 8h scheduled for that single day.
        var taskId = Guid.NewGuid();
        var workSessionRepository = new FakeWorkSessionRepository();
        var startedAt = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);
        var session = WorkSession.Start(taskId, startedAt);
        session.RecordActivity(ActivitySource.Human, startedAt, startedAt.AddHours(3));
        session.End(startedAt.AddHours(3));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var workScheduleStore = new FakeWorkScheduleStore();
        await workScheduleStore.SaveAsync(
            WorkSchedule.Create(new TimeOnly(9, 0), new TimeOnly(18, 0), new TimeOnly(12, 0), new TimeOnly(13, 0),
                [DayOfWeek.Monday]),
            CancellationToken.None);

        var unmappedTimeEntryRepository = new FakeUnmappedTimeEntryRepository();
        var useCase = new CalculateUnmappedTimeUseCase(workSessionRepository, workScheduleStore, unmappedTimeEntryRepository);

        TimeSpan? result = await useCase.ExecuteAsync(taskId);

        // 8h scheduled - 3h mapped = 5h unmapped.
        Assert.Equal(TimeSpan.FromHours(5), result);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMappedExceedsScheduled_ReturnsZeroNotNegative()
    {
        var taskId = Guid.NewGuid();
        var workSessionRepository = new FakeWorkSessionRepository();
        var startedAt = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);
        var session = WorkSession.Start(taskId, startedAt);
        session.RecordActivity(ActivitySource.Human, startedAt, startedAt.AddHours(10));
        session.End(startedAt.AddHours(10));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var workScheduleStore = new FakeWorkScheduleStore();
        await workScheduleStore.SaveAsync(
            WorkSchedule.Create(new TimeOnly(9, 0), new TimeOnly(18, 0), new TimeOnly(12, 0), new TimeOnly(13, 0),
                [DayOfWeek.Monday]),
            CancellationToken.None);

        var unmappedTimeEntryRepository = new FakeUnmappedTimeEntryRepository();
        var useCase = new CalculateUnmappedTimeUseCase(workSessionRepository, workScheduleStore, unmappedTimeEntryRepository);

        TimeSpan? result = await useCase.ExecuteAsync(taskId);

        Assert.Equal(TimeSpan.Zero, result);
    }

    [Fact]
    public async Task ExecuteAsync_WithOverlappingHumanAndAiActivity_DoesNotDoubleCountTheOverlap()
    {
        // Monday 2026-01-05, 9-18 with 12-13 lunch = 8h scheduled.
        // Human 9-12 (3h) overlapping AI 11-13 (2h) -> merged mapped time is 9-13 = 4h, not 5h.
        var taskId = Guid.NewGuid();
        var workSessionRepository = new FakeWorkSessionRepository();
        var startedAt = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);
        var session = WorkSession.Start(taskId, startedAt);
        session.RecordActivity(ActivitySource.Human, startedAt, startedAt.AddHours(3));
        session.RecordActivity(ActivitySource.Ai, startedAt.AddHours(2), startedAt.AddHours(4));
        session.End(startedAt.AddHours(4));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var workScheduleStore = new FakeWorkScheduleStore();
        await workScheduleStore.SaveAsync(
            WorkSchedule.Create(new TimeOnly(9, 0), new TimeOnly(18, 0), new TimeOnly(12, 0), new TimeOnly(13, 0),
                [DayOfWeek.Monday]),
            CancellationToken.None);

        var unmappedTimeEntryRepository = new FakeUnmappedTimeEntryRepository();
        var useCase = new CalculateUnmappedTimeUseCase(workSessionRepository, workScheduleStore, unmappedTimeEntryRepository);

        TimeSpan? result = await useCase.ExecuteAsync(taskId);

        // 8h scheduled - 4h mapped (union, not 5h naive sum) = 4h unmapped.
        Assert.Equal(TimeSpan.FromHours(4), result);
    }

    [Fact]
    public async Task ExecuteAsync_DiscountsAlreadyRecordedUnmappedTimeEntries()
    {
        // 8h scheduled - 3h mapped - 2h already recorded = 3h remaining.
        var taskId = Guid.NewGuid();
        var workSessionRepository = new FakeWorkSessionRepository();
        var startedAt = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);
        var session = WorkSession.Start(taskId, startedAt);
        session.RecordActivity(ActivitySource.Human, startedAt, startedAt.AddHours(3));
        session.End(startedAt.AddHours(3));
        await workSessionRepository.AddAsync(session, CancellationToken.None);

        var workScheduleStore = new FakeWorkScheduleStore();
        await workScheduleStore.SaveAsync(
            WorkSchedule.Create(new TimeOnly(9, 0), new TimeOnly(18, 0), new TimeOnly(12, 0), new TimeOnly(13, 0),
                [DayOfWeek.Monday]),
            CancellationToken.None);

        var unmappedTimeEntryRepository = new FakeUnmappedTimeEntryRepository();
        await unmappedTimeEntryRepository.AddAsync(
            UnmappedTimeEntry.Create(taskId, TimeSpan.FromHours(2), "Client call.", DateTimeOffset.UtcNow),
            CancellationToken.None);

        var useCase = new CalculateUnmappedTimeUseCase(workSessionRepository, workScheduleStore, unmappedTimeEntryRepository);

        TimeSpan? result = await useCase.ExecuteAsync(taskId);

        Assert.Equal(TimeSpan.FromHours(3), result);
    }
}
