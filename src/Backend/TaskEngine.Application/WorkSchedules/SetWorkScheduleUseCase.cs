using TaskEngine.Application.Abstractions;

namespace TaskEngine.Application.WorkSchedules;

/// <summary>
/// Creates (or replaces) the single work-hours template used to calculate scheduled hours.
/// </summary>
public sealed class SetWorkScheduleUseCase
{
    private readonly IWorkScheduleStore _workScheduleStore;

    public SetWorkScheduleUseCase(IWorkScheduleStore workScheduleStore)
    {
        _workScheduleStore = workScheduleStore;
    }

    public async Task ExecuteAsync(SetWorkScheduleRequest request, CancellationToken cancellationToken = default)
    {
        Domain.Entities.WorkSchedule schedule = Domain.Entities.WorkSchedule.Create(
            request.StartTime,
            request.EndTime,
            request.LunchStart,
            request.LunchEnd,
            request.WorkDays);

        await _workScheduleStore.SaveAsync(schedule, cancellationToken);
    }
}
