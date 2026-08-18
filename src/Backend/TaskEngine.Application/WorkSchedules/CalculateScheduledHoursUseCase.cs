using TaskEngine.Application.Abstractions;

namespace TaskEngine.Application.WorkSchedules;

/// <summary>
/// Calculates the scheduled (expected) work hours over a date range from the currently saved
/// <see cref="Domain.Entities.WorkSchedule"/> template. Returns <c>null</c> when no template has
/// been configured yet - it is up to the caller to decide how to handle that case.
/// </summary>
public sealed class CalculateScheduledHoursUseCase
{
    private readonly IWorkScheduleStore _workScheduleStore;

    public CalculateScheduledHoursUseCase(IWorkScheduleStore workScheduleStore)
    {
        _workScheduleStore = workScheduleStore;
    }

    public async Task<TimeSpan?> ExecuteAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        Domain.Entities.WorkSchedule? schedule = await _workScheduleStore.GetAsync(cancellationToken);
        return schedule?.CalculateHoursInPeriod(start, end);
    }
}
