using System.Text.Json;
using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Infrastructure.Persistence;

/// <summary>
/// <see cref="IWorkScheduleStore"/> implementation built on top of <see cref="IAppSettingsStore"/>
/// (same pattern as <see cref="DpapiCredentialStore"/>): the single <see cref="WorkSchedule"/>
/// template is serialized to a <see cref="WorkScheduleData"/> DTO and persisted under a fixed key
/// in the <c>app_settings</c> table, rather than getting a dedicated table of its own.
/// </summary>
public sealed class AppSettingsWorkScheduleStore : IWorkScheduleStore
{
    private const string SettingsKey = "work_schedule";

    private readonly IAppSettingsStore _appSettingsStore;

    public AppSettingsWorkScheduleStore(IAppSettingsStore appSettingsStore)
    {
        _appSettingsStore = appSettingsStore;
    }

    public async Task<WorkSchedule?> GetAsync(CancellationToken cancellationToken)
    {
        var json = await _appSettingsStore.GetAsync(SettingsKey, cancellationToken);
        if (json is null)
        {
            return null;
        }

        var data = JsonSerializer.Deserialize<WorkScheduleData>(json)
            ?? throw new InvalidOperationException("Stored work schedule data is invalid.");

        return WorkSchedule.Create(data.StartTime, data.EndTime, data.LunchStart, data.LunchEnd, data.WorkDays);
    }

    public async Task SaveAsync(WorkSchedule schedule, CancellationToken cancellationToken)
    {
        var data = new WorkScheduleData(
            schedule.StartTime,
            schedule.EndTime,
            schedule.LunchStart,
            schedule.LunchEnd,
            schedule.WorkDays.ToArray());

        var json = JsonSerializer.Serialize(data);
        await _appSettingsStore.SetAsync(SettingsKey, json, cancellationToken);
    }

    private sealed record WorkScheduleData(
        TimeOnly StartTime,
        TimeOnly EndTime,
        TimeOnly? LunchStart,
        TimeOnly? LunchEnd,
        DayOfWeek[] WorkDays);
}
