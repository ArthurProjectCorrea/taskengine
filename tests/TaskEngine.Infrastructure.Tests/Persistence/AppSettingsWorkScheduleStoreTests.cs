using TaskEngine.Domain.Entities;
using TaskEngine.Infrastructure.Persistence;

namespace TaskEngine.Infrastructure.Tests.Persistence;

public class AppSettingsWorkScheduleStoreTests
{
    [Fact]
    public async Task GetAsync_BeforeAnySaveAsync_ReturnsNull()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var appSettingsStore = new SqliteAppSettingsStore(db.PathProvider);
        var store = new AppSettingsWorkScheduleStore(appSettingsStore);

        WorkSchedule? result = await store.GetAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsAllFieldsIncludingLunchAndWorkDays()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var appSettingsStore = new SqliteAppSettingsStore(db.PathProvider);
        var store = new AppSettingsWorkScheduleStore(appSettingsStore);
        WorkSchedule schedule = WorkSchedule.Create(
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            new TimeOnly(12, 0),
            new TimeOnly(13, 0),
            [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday]);

        await store.SaveAsync(schedule, CancellationToken.None);
        WorkSchedule? result = await store.GetAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(schedule.StartTime, result.StartTime);
        Assert.Equal(schedule.EndTime, result.EndTime);
        Assert.Equal(schedule.LunchStart, result.LunchStart);
        Assert.Equal(schedule.LunchEnd, result.LunchEnd);
        Assert.Equal(schedule.WorkDays.Order(), result.WorkDays.Order());
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsScheduleWithoutLunch()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var appSettingsStore = new SqliteAppSettingsStore(db.PathProvider);
        var store = new AppSettingsWorkScheduleStore(appSettingsStore);
        WorkSchedule schedule = WorkSchedule.Create(
            new TimeOnly(8, 0),
            new TimeOnly(16, 0),
            null,
            null,
            [DayOfWeek.Saturday]);

        await store.SaveAsync(schedule, CancellationToken.None);
        WorkSchedule? result = await store.GetAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.LunchStart);
        Assert.Null(result.LunchEnd);
        Assert.Equal([DayOfWeek.Saturday], result.WorkDays);
    }

    [Fact]
    public async Task SaveAsync_WhenScheduleAlreadyExists_OverwritesIt()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var appSettingsStore = new SqliteAppSettingsStore(db.PathProvider);
        var store = new AppSettingsWorkScheduleStore(appSettingsStore);
        WorkSchedule first = WorkSchedule.Create(
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            null,
            null,
            [DayOfWeek.Monday]);
        WorkSchedule second = WorkSchedule.Create(
            new TimeOnly(10, 0),
            new TimeOnly(19, 0),
            null,
            null,
            [DayOfWeek.Tuesday]);

        await store.SaveAsync(first, CancellationToken.None);
        await store.SaveAsync(second, CancellationToken.None);
        WorkSchedule? result = await store.GetAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(second.StartTime, result.StartTime);
        Assert.Equal([DayOfWeek.Tuesday], result.WorkDays);
    }
}
