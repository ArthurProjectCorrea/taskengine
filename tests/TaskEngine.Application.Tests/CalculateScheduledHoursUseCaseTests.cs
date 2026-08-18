using TaskEngine.Application.WorkSchedules;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Tests;

public class CalculateScheduledHoursUseCaseTests
{
    // 2024-01-01 is a Monday.
    private static readonly DateOnly Monday = new(2024, 1, 1);
    private static readonly DateOnly Tuesday = new(2024, 1, 2);

    [Fact]
    public async Task ExecuteAsync_WhenNoScheduleConfigured_ReturnsNull()
    {
        var store = new FakeWorkScheduleStore();
        var useCase = new CalculateScheduledHoursUseCase(store);

        TimeSpan? result = await useCase.ExecuteAsync(Monday, Tuesday);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteAsync_WhenScheduleConfigured_DelegatesToDomainCalculation()
    {
        var store = new FakeWorkScheduleStore();
        WorkSchedule schedule = WorkSchedule.Create(
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            new TimeOnly(12, 0),
            new TimeOnly(13, 0),
            [DayOfWeek.Monday, DayOfWeek.Tuesday]);
        await store.SaveAsync(schedule, CancellationToken.None);
        var useCase = new CalculateScheduledHoursUseCase(store);

        TimeSpan? result = await useCase.ExecuteAsync(Monday, Tuesday);

        Assert.Equal(schedule.CalculateHoursInPeriod(Monday, Tuesday), result);
        Assert.Equal(TimeSpan.FromHours(16), result);
    }
}
