namespace TaskEngine.Application.WorkSchedules;

/// <summary>
/// Raw input for <see cref="SetWorkScheduleUseCase"/>, mirroring
/// <see cref="Domain.Entities.WorkSchedule.Create"/>'s parameters.
/// </summary>
public sealed record SetWorkScheduleRequest(
    TimeOnly StartTime,
    TimeOnly EndTime,
    TimeOnly? LunchStart,
    TimeOnly? LunchEnd,
    IReadOnlyList<DayOfWeek> WorkDays);
