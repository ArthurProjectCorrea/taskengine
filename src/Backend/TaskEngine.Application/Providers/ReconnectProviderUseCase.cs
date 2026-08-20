using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Tasks;
using TaskEngine.Domain.Entities;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Application.Providers;

/// <summary>
/// Reconnects a previously frozen provider (RF-013/RF-014, RN-008/RN-009, ERS-Tarefas.md):
/// unfreezes it, sweeps locally completed tasks still pending sync
/// (<see cref="TaskStatus.DonePendingSync"/>, CA-014.2) and pushes their status to the provider,
/// then runs a normal <see cref="SyncTasksUseCase"/> pull. Pushing only the status here (not the
/// worked time) is a known gap: full worklog reporting is added on top of this same sweep once
/// <c>ITaskProviderClient</c> grows a time-reporting method (see the item that wires
/// <c>ConcludeTaskUseCase</c> to sync status *and* time - #7/#10) - until then a reconnect clears
/// the pending flag with status only, which is still strictly better than leaving it unsynced.
/// </summary>
public sealed class ReconnectProviderUseCase
{
    private readonly IAppSettingsStore _appSettingsStore;
    private readonly ITaskRepository _taskRepository;
    private readonly IProviderClientFactory _providerClientFactory;
    private readonly SyncTasksUseCase _syncTasksUseCase;

    public ReconnectProviderUseCase(
        IAppSettingsStore appSettingsStore,
        ITaskRepository taskRepository,
        IProviderClientFactory providerClientFactory,
        SyncTasksUseCase syncTasksUseCase)
    {
        _appSettingsStore = appSettingsStore;
        _taskRepository = taskRepository;
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
                await providerClient.UpdateStatusAsync(reference, TaskStatus.Done, cancellationToken);

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
}
