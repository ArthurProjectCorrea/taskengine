using TaskEngine.Domain.Entities;

namespace TaskEngine.Domain.Tests;

public class WorkScheduleTests
{
    // 2024-01-01 is a Monday; the following days step through the rest of that week.
    private static readonly DateOnly Monday = new(2024, 1, 1);
    private static readonly DateOnly Tuesday = new(2024, 1, 2);
    private static readonly DateOnly Wednesday = new(2024, 1, 3);
    private static readonly DateOnly Friday = new(2024, 1, 5);
    private static readonly DateOnly Saturday = new(2024, 1, 6);
    private static readonly DateOnly Sunday = new(2024, 1, 7);
    private static readonly DateOnly NextMonday = new(2024, 1, 8);

    private static readonly DayOfWeek[] WeekdaysOnly =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
    ];

    [Fact]
    public void Create_ThrowsWhenEndTimeIsNotAfterStartTime()
    {
        Assert.Throws<ArgumentException>(() => WorkSchedule.Create(
            new TimeOnly(9, 0),
            new TimeOnly(9, 0),
            null,
            null,
            WeekdaysOnly));
    }

    [Fact]
    public void Create_ThrowsWhenOnlyLunchStartIsProvided()
    {
        Assert.Throws<ArgumentException>(() => WorkSchedule.Create(
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            new TimeOnly(12, 0),
            null,
            WeekdaysOnly));
    }

    [Fact]
    public void Create_ThrowsWhenOnlyLunchEndIsProvided()
    {
        Assert.Throws<ArgumentException>(() => WorkSchedule.Create(
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            null,
            new TimeOnly(13, 0),
            WeekdaysOnly));
    }

    [Fact]
    public void Create_ThrowsWhenLunchEndIsNotAfterLunchStart()
    {
        Assert.Throws<ArgumentException>(() => WorkSchedule.Create(
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            new TimeOnly(13, 0),
            new TimeOnly(13, 0),
            WeekdaysOnly));
    }

    [Fact]
    public void Create_ThrowsWhenLunchStartsBeforeStartTime()
    {
        Assert.Throws<ArgumentException>(() => WorkSchedule.Create(
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            new TimeOnly(8, 0),
            new TimeOnly(9, 30),
            WeekdaysOnly));
    }

    [Fact]
    public void Create_ThrowsWhenLunchEndsAfterEndTime()
    {
        Assert.Throws<ArgumentException>(() => WorkSchedule.Create(
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            new TimeOnly(17, 30),
            new TimeOnly(18, 30),
            WeekdaysOnly));
    }

    [Fact]
    public void Create_ThrowsWhenWorkDaysIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => WorkSchedule.Create(
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            null,
            null,
            []));
    }

    [Fact]
    public void CalculateHoursInPeriod_ThrowsWhenEndIsBeforeStart()
    {
        WorkSchedule schedule = WorkSchedule.Create(
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            new TimeOnly(12, 0),
            new TimeOnly(13, 0),
            WeekdaysOnly);

        Assert.Throws<ArgumentException>(() => schedule.CalculateHoursInPeriod(Tuesday, Monday));
    }

    [Fact]
    public void CalculateHoursInPeriod_SingleWorkDay_ReturnsExpedientMinusLunch()
    {
        WorkSchedule schedule = WorkSchedule.Create(
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            new TimeOnly(12, 0),
            new TimeOnly(13, 0),
            WeekdaysOnly);

        TimeSpan result = schedule.CalculateHoursInPeriod(Monday, Monday);

        Assert.Equal(TimeSpan.FromHours(8), result);
    }

    [Fact]
    public void CalculateHoursInPeriod_WithoutLunch_CountsFullExpedient()
    {
        WorkSchedule schedule = WorkSchedule.Create(
            new TimeOnly(9, 0),
            new TimeOnly(17, 0),
            null,
            null,
            WeekdaysOnly);

        TimeSpan result = schedule.CalculateHoursInPeriod(Monday, Monday);

        Assert.Equal(TimeSpan.FromHours(8), result);
    }

    [Fact]
    public void CalculateHoursInPeriod_PeriodCoveringWeekend_ExcludesNonWorkDays()
    {
        WorkSchedule schedule = WorkSchedule.Create(
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            new TimeOnly(12, 0),
            new TimeOnly(13, 0),
            WeekdaysOnly);

        // Friday through next Monday: Fri + Mon are work days, Sat + Sun count zero.
        TimeSpan result = schedule.CalculateHoursInPeriod(Friday, NextMonday);

        Assert.Equal(TimeSpan.FromHours(16), result);
    }

    [Fact]
    public void CalculateHoursInPeriod_MultipleConsecutiveWorkDays_SumsEachDay()
    {
        WorkSchedule schedule = WorkSchedule.Create(
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            new TimeOnly(12, 0),
            new TimeOnly(13, 0),
            WeekdaysOnly);

        TimeSpan result = schedule.CalculateHoursInPeriod(Monday, Wednesday);

        Assert.Equal(TimeSpan.FromHours(24), result);
    }

    [Fact]
    public void CalculateHoursInPeriod_DayOutsideTemplate_CountsZero()
    {
        WorkSchedule schedule = WorkSchedule.Create(
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            new TimeOnly(12, 0),
            new TimeOnly(13, 0),
            WeekdaysOnly);

        TimeSpan result = schedule.CalculateHoursInPeriod(Saturday, Sunday);

        Assert.Equal(TimeSpan.Zero, result);
    }
}
