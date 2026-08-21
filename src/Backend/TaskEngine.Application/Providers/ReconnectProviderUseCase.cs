using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Tasks;
using TaskEngine.Domain.Entities;
using TaskEngine.Domain.TimeTracking;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Application.Providers;

/// <summary>
/// Reconnects a previously frozen provider (RF-013/RF-014, RN-008/RN-009, ERS-Tarefas.md):
/// unfreezes it, sweeps locally completed tasks still pending sync
/// (<see cref="TaskStatus.DonePendingSync"/>, CA-014.2) and pushes their status *and* final time to
/// the provider (recomputed from each task's own <see cref="WorkSession"/>s the same way
/// <c>ConcludeTaskUseCase</c> does, via <see cref="ITaskProviderClient.ReportCompletionAsync"/>),
/// then runs a normal <see cref="SyncTasksUseCase"/> pull.
/// </summary>
public sealed class ReconnectProviderUseCase
{
    private readonly IAppSettingsStore _appSettingsStore;
    private readonly ITaskRepository _taskRepository;
    private readonly IWorkSessionRepository _workSessionRepository;
    private readonly IProviderClientFactory _providerClientFactory;
    private readonly SyncTasksUseCase _syncTasksUseCase;

    public ReconnectProviderUseCase(
        IAppSettingsStore appSettingsStore,
        ITaskRepository taskRepository,
        IWorkSessionRepository workSessionRepository,
        IProviderClientFactory providerClientFactory,
        SyncTasksUseCase syncTasksUseCase)
    {
        _appSettingsStore = appSettingsStore;
        _taskRepository = taskRepository;
        _workSessionRepository = workSessionRepository;
        _providerClientFactory = providerClientFactory;
        _syncTasksUseCase = syncTasksUseCase;
    }

    public async Task<IReadOnlyList<TaskDto>> ExecuteAsync(string providerId, CancellationToken cancellationToken = default)
    {
        await _appSettingsStore.DeleteAsync(ProviderSettingsKeys.Frozen(providerId), cancellationToken);

        await SyncPendingCompletionsAsync(providerId, cancellationToken);

        return await _syncTasksUseCase.ExecuteAsync(providerId, cancellationToken);
    }

    private async Task SyncPendingCompletionsAsync(string providerId, CancellationToken cancellationToken)
    {
        IReadOnlyList<TaskItem> localTasks = await _taskRepository.ListAsync(cancellationToken);
        List<TaskItem> pending = localTasks
            .Where(t => t.ProviderId == providerId && t.Status == TaskStatus.DonePendingSync && t.ProviderTaskId is not null)
            .ToList();

        if (pending.Count == 0)
        {
            return;
        }

        ITaskProviderClient providerClient = await _providerClientFactory.CreateAsync(providerId, cancellationToken);

        foreach (TaskItem task in pending)
        {
            try
            {
                var reference = new ProviderTaskReference(providerId, task.ProviderTaskId!, Url: null);
                TimeSpan totalDuration = await ComputeTotalDurationAsync(task.Id, cancellationToken);
                await providerClient.ReportCompletionAsync(reference, totalDuration, cancellationToken);

                task.MarkSynced();
                await _taskRepository.UpdateAsync(task, cancellationToken);
            }
            catch (Exception)
            {
                // Still unreachable/revoked again (RNF-003: no data loss on a failed sync) - the
                // task stays DonePendingSync and the next reconnect will retry it (CA-014.2).
            }
        }
    }

    /// <summary>
    /// Recomputes the task's final total time (RN-005/RN-007/RN-012) from the activity selection
    /// <c>ConcludeTaskUseCase</c> already persisted onto each <see cref="WorkSession"/> at
    /// conclusion time - same union-of-intervals approach, just reloaded from storage here instead
    /// of computed in the same call.
    /// </summary>
    private async Task<TimeSpan> ComputeTotalDurationAsync(Guid taskId, CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkSession> sessions = await _workSessionRepository.ListByTaskIdAsync(taskId, cancellationToken);

        var intervals = new List<TimeInterval>();
        foreach (WorkSession session in sessions)
        {
            if (session.Type != WorkSessionType.Active)
            {
                continue;
            }

            foreach (ActivityInterval activity in session.Activities)
            {
                if (activity.SelectedAtConclusion)
                {
                    intervals.Add(new TimeInterval(activity.StartedAt, activity.EndedAt));
                }
            }
        }

        return TimeIntervalMerger.TotalDuration(intervals);
    }
}
