using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;
using TaskEngine.Domain.Entities;
using TaskEngine.Domain.TimeTracking;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Application.Tasks;

/// <summary>
/// Concludes a task with the user's selection of which activity items actually belong to it
/// (RF-007/Schema-003, ERS-Tarefas.md): attributes each selected item from the task-independent
/// monitored activity stream (<see cref="IMonitoredActivityRepository"/>, RF-004) onto whichever
/// <see cref="WorkSession"/> its period falls into - the bridge <see cref="WorkSession.AttributeSelectedActivity"/>
/// exists for - then computes the final human/AI/total time strictly from the selected items in
/// active periods only (RN-005/RN-012 - pause periods, see <see cref="WorkSessionType.Pause"/>,
/// never contribute), then syncs status and time to the provider (RF-009) via
/// <see cref="ITaskProviderClient.ReportCompletionAsync"/>. <see cref="TimeIntervalMerger"/> is
/// used twice: once per origin for <see cref="ConcludeTaskResult.HumanDuration"/>/
/// <see cref="ConcludeTaskResult.AiDuration"/>, and once across both origins together for
/// <see cref="ConcludeTaskResult.TotalDuration"/> (RN-007 - a period where human and AI activity
/// overlap is not double-counted in the total, even though it still counts once for each origin).
/// </summary>
public sealed class ConcludeTaskUseCase
{
    private readonly ITaskRepository _taskRepository;
    private readonly IWorkSessionRepository _workSessionRepository;
    private readonly IMonitoredActivityRepository _monitoredActivityRepository;
    private readonly IProviderClientFactory _providerClientFactory;
    private readonly IAppSettingsStore _appSettingsStore;

    public ConcludeTaskUseCase(
        ITaskRepository taskRepository,
        IWorkSessionRepository workSessionRepository,
        IMonitoredActivityRepository monitoredActivityRepository,
        IProviderClientFactory providerClientFactory,
        IAppSettingsStore appSettingsStore)
    {
        _taskRepository = taskRepository;
        _workSessionRepository = workSessionRepository;
        _monitoredActivityRepository = monitoredActivityRepository;
        _providerClientFactory = providerClientFactory;
        _appSettingsStore = appSettingsStore;
    }

    public async Task<ConcludeTaskResult> ExecuteAsync(
        ConcludeTaskRequest request, CancellationToken cancellationToken = default)
    {
        TaskItem task = await _taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new InvalidOperationException($"Task '{request.TaskId}' was not found.");

        if (task.Status != TaskStatus.InProgress && task.Status != TaskStatus.Paused)
        {
            // Mirrors TaskItem.Complete()'s own guard, checked up front (before any session work
            // or provider call) so an invalid conclusion attempt (e.g. already Done - RN-006)
            // leaves everything untouched.
            throw new InvalidOperationException($"Cannot complete a task in status '{task.Status}'.");
        }

        if (task.ProviderId is not null)
        {
            // RF-013/CA-013.1: conclusion is blocked outright while the provider is frozen -
            // checked before any mutation so the task is left exactly as it was ("manter a tarefa
            // como está"). This is different from RF-014's offline fallback below, which only
            // applies to a *newly* detected failure (e.g. a fresh 401, or no connectivity), not a
            // provider already known to be frozen.
            var frozen = await _appSettingsStore.GetAsync(ProviderSettingsKeys.Frozen(task.ProviderId), cancellationToken);
            if (frozen is not null)
            {
                throw new ProviderFrozenException(task.ProviderId);
            }
        }

        IReadOnlyList<WorkSession> sessions = await _workSessionRepository.ListByTaskIdAsync(request.TaskId, cancellationToken);

        // Everything below mutates in-memory only - nothing is persisted until the provider
        // outcome (if any) is resolved, so a freshly-detected ProviderFrozenException still
        // leaves local state untouched.
        var humanIntervals = new List<TimeInterval>();
        var aiIntervals = new List<TimeInterval>();
        var allIntervals = new List<TimeInterval>();

        foreach (WorkSession session in sessions)
        {
            if (session.Type == WorkSessionType.Active)
            {
                await AttributeMonitoredActivitiesAsync(session, request.SelectedActivityIds, cancellationToken);
                session.ApplyActivitySelection(request.SelectedActivityIds);
                AccumulateSelectedIntervals(session, humanIntervals, aiIntervals, allIntervals);
            }

            // A task can be concluded while its current period is still open (in progress, or
            // paused without having been resumed first) - conclusion closes it out either way, so
            // no work session is left dangling open under a Done task.
            if (session.IsOpen)
            {
                session.End(DateTimeOffset.UtcNow);
            }
        }

        var humanDuration = TimeIntervalMerger.TotalDuration(humanIntervals);
        var aiDuration = TimeIntervalMerger.TotalDuration(aiIntervals);
        var totalDuration = TimeIntervalMerger.TotalDuration(allIntervals);

        await SyncOrCompleteOfflineAsync(task, totalDuration, cancellationToken);

        foreach (WorkSession session in sessions)
        {
            await _workSessionRepository.UpdateAsync(session, cancellationToken);
        }

        await _taskRepository.UpdateAsync(task, cancellationToken);

        return new ConcludeTaskResult(ToDto(task), humanDuration, aiDuration, totalDuration);
    }

    /// <summary>
    /// Decides between <see cref="TaskItem.Complete"/> (synced) and <see cref="TaskItem.CompleteOffline"/>
    /// (RF-014/RN-009 - pending sync, picked up later by <c>ReconnectProviderUseCase</c>). A task
    /// with no provider link has nothing to sync, so it always completes normally. A freshly
    /// detected <see cref="ProviderFrozenException"/> (e.g. a 401 during this very call) is not
    /// swallowed into an offline completion - CA-013.1 blocks the action outright instead.
    /// </summary>
    private async Task SyncOrCompleteOfflineAsync(TaskItem task, TimeSpan totalDuration, CancellationToken cancellationToken)
    {
        if (task.ProviderId is null || task.ProviderTaskId is null)
        {
            task.Complete();
            return;
        }

        try
        {
            ITaskProviderClient providerClient = await _providerClientFactory.CreateAsync(task.ProviderId, cancellationToken);
            var reference = new ProviderTaskReference(task.ProviderId, task.ProviderTaskId, Url: null);
            await providerClient.ReportCompletionAsync(reference, totalDuration, cancellationToken);

            task.Complete();
        }
        catch (ProviderFrozenException)
        {
            throw;
        }
        catch (Exception)
        {
            // Offline, or any other provider failure (timeout, GraphQL error, etc.) - RF-014:
            // conclude locally anyway, deferred sync via ReconnectProviderUseCase (RNF-003: no
            // loss of the already-computed local time/selection).
            task.CompleteOffline();
        }
    }

    /// <summary>
    /// Queries <see cref="IMonitoredActivityRepository"/> for the monitored activity overlapping
    /// <paramref name="session"/>'s own [StartedAt, EndedAt ?? now] window (RN-012 excludes pauses,
    /// so this is only called for <see cref="WorkSessionType.Active"/> sessions) and attributes
    /// whichever of those items are in <paramref name="selectedActivityIds"/> onto the session -
    /// the same period-matching logic <c>ConcludeTaskModalViewModel.OpenAsync</c> uses to build the
    /// checklist in the first place, so a selected id always resolves back to a real activity here.
    /// </summary>
    private async Task AttributeMonitoredActivitiesAsync(
        WorkSession session, IReadOnlySet<Guid> selectedActivityIds, CancellationToken cancellationToken)
    {
        DateTimeOffset from = session.StartedAt;
        DateTimeOffset to = session.EndedAt ?? DateTimeOffset.UtcNow;
        if (to <= from)
        {
            return;
        }

        IReadOnlyList<ActivityInterval> monitored =
            await _monitoredActivityRepository.ListByPeriodAsync(from, to, cancellationToken);

        foreach (ActivityInterval activity in monitored)
        {
            if (selectedActivityIds.Contains(activity.Id))
            {
                session.AttributeSelectedActivity(activity);
            }
        }
    }

    private static void AccumulateSelectedIntervals(
        WorkSession session, List<TimeInterval> humanIntervals, List<TimeInterval> aiIntervals, List<TimeInterval> allIntervals)
    {
        foreach (ActivityInterval activity in session.Activities)
        {
            if (!activity.SelectedAtConclusion)
            {
                continue;
            }

            var interval = new TimeInterval(activity.StartedAt, activity.EndedAt);
            allIntervals.Add(interval);

            List<TimeInterval> bucket = activity.Source == ActivitySource.Human ? humanIntervals : aiIntervals;
            bucket.Add(interval);
        }
    }

    private static TaskDto ToDto(TaskItem task)
    {
        return new TaskDto(
            task.Id,
            task.Title,
            task.Description,
            task.Status.ToString(),
            task.CreatedAt,
            ProviderTaskId: task.ProviderTaskId,
            Priority: task.Priority,
            ProviderUrl: task.ProviderUrl);
    }
}
