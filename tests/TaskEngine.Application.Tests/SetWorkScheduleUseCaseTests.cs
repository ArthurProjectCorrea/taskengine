using TaskEngine.Application.WorkSchedules;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Tests;

public class SetWorkScheduleUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidRequest_SavesTheSchedule()
    {
        var store = new FakeWorkScheduleStore();
        var useCase = new SetWorkScheduleUseCase(store);
        var request = new SetWorkScheduleRequest(
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            new TimeOnly(12, 0),
            new TimeOnly(13, 0),
            [DayOfWeek.Monday, DayOfWeek.Tuesday]);

        await useCase.ExecuteAsync(request);

        WorkSchedule? saved = await store.GetAsync(CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal(new TimeOnly(9, 0), saved.StartTime);
        Assert.Equal(new TimeOnly(18, 0), saved.EndTime);
        Assert.Equal(new TimeOnly(12, 0), saved.LunchStart);
        Assert.Equal(new TimeOnly(13, 0), saved.LunchEnd);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Tuesday], saved.WorkDays.Order());
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidRequest_ThrowsAndDoesNotSave()
    {
        var store = new FakeWorkScheduleStore();
        var useCase = new SetWorkScheduleUseCase(store);
        var request = new SetWorkScheduleRequest(
            new TimeOnly(18, 0),
            new TimeOnly(9, 0),
            null,
            null,
            [DayOfWeek.Monday]);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(request));

        Assert.Null(await store.GetAsync(CancellationToken.None));
    }
}
