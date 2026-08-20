using System.Globalization;
using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;
using TaskEngine.Application.WorkSessions;
using TaskEngine.Domain.Entities;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Application.Tasks;

/// <summary>
/// Pulls the tasks assigned to the user on a connected provider (RF-001) and upserts them
/// locally: tasks already known (matched by <see cref="TaskItem.ProviderTaskId"/>) get their
/// priority/status refreshed, new ones are created - except a never-before-seen task that is
/// already done on the provider and was created before the provider was connected, which RN-001
/// excludes entirely. Also detects provider-side status changes that should pause/resume/complete
/// local time tracking (RF-015/CA-015.2), reusing <see cref="PauseWorkSessionUseCase"/>/
/// <see cref="StartWorkSessionUseCase"/>/<see cref="EndWorkSessionUseCase"/> rather than
/// duplicating that orchestration. Automatic periodic sync (RF-002) is a Desktop/Infrastructure
/// concern (a timer calling this use case on demand) and is out of scope here.
/// </summary>
public sealed class SyncTasksUseCase
{
    private readonly ITaskRepository _taskRepository;
    private readonly IProviderClientFactory _providerClientFactory;
    private readonly IAppSettingsStore _appSettingsStore;
    private readonly PauseWorkSessionUseCase _pauseWorkSessionUseCase;
    private readonly StartWorkSessionUseCase _startWorkSessionUseCase;
    private readonly EndWorkSessionUseCase _endWorkSessionUseCase;

    public SyncTasksUseCase(
        ITaskRepository taskRepository,
        IProviderClientFactory providerClientFactory,
        IAppSettingsStore appSettingsStore,
        PauseWorkSessionUseCase pauseWorkSessionUseCase,
        StartWorkSessionUseCase startWorkSessionUseCase,
        EndWorkSessionUseCase endWorkSessionUseCase)
    {
        _taskRepository = taskRepository;
        _providerClientFactory = providerClientFactory;
        _appSettingsStore = appSettingsStore;
        _pauseWorkSessionUseCase = pauseWorkSessionUseCase;
        _startWorkSessionUseCase = startWorkSessionUseCase;
        _endWorkSessionUseCase = endWorkSessionUseCase;
    }

    public async Task<IReadOnlyList<TaskDto>> ExecuteAsync(string providerId, CancellationToken cancellationToken = default)
    {
        // RF-013/CA-013.1: sync is blocked while the provider is frozen (access revoked, or
        // explicitly disconnected), until it is reconnected.
        var frozen = await _appSettingsStore.GetAsync(ProviderSettingsKeys.Frozen(providerId), cancellationToken);
        if (frozen is not null)
        {
            throw new ProviderFrozenException(providerId);
        }

        ITaskProviderClient providerClient = await _providerClientFactory.CreateAsync(providerId, cancellationToken);
        IReadOnlyList<ProviderTaskSummary> remoteTasks = await providerClient.ListAssignedTasksAsync(cancellationToken);

        DateTimeOffset connectedAt = await GetOrEstablishConnectedAtAsync(providerId, cancellationToken);

        IReadOnlyList<TaskItem> localTasks = await _taskRepository.ListAsync(cancellationToken);
        Dictionary<string, TaskItem> localByProviderTaskId = localTasks
            .Where(t => t.ProviderId == providerId && t.ProviderTaskId is not null)
            .ToDictionary(t => t.ProviderTaskId!, t => t);

        var result = new List<TaskDto>();

        foreach (ProviderTaskSummary remote in remoteTasks)
        {
            TaskItem? task = localByProviderTaskId.TryGetValue(remote.ExternalId, out var existing)
                ? await UpsertExistingAsync(existing, remote, cancellationToken)
                : await UpsertNewAsync(remote, providerId, connectedAt, cancellationToken);

            if (task is not null)
            {
                result.Add(ToDto(task));
            }
        }

        return result;
    }

    private async Task<TaskItem> UpsertExistingAsync(TaskItem task, ProviderTaskSummary remote, CancellationToken cancellationToken)
    {
        task.SetPriority(remote.Priority);
        task.SetProviderStatusName(remote.StatusName);
        await _taskRepository.UpdateAsync(task, cancellationToken);

        await ApplyRemoteStatusTransitionAsync(task, remote, cancellationToken);

        // Re-fetch: the status transition above may have been persisted by a composed use case
        // operating on its own freshly-fetched copy of the task, so this in-memory `task` can be
        // stale by now (e.g. still Paused after EndWorkSessionUseCase already completed it).
        return await _taskRepository.GetByIdAsync(task.Id, cancellationToken) ?? task;
    }

    private async Task<TaskItem?> UpsertNewAsync(
        ProviderTaskSummary remote, string providerId, DateTimeOffset connectedAt, CancellationToken cancellationToken)
    {
        // RN-001: a task never seen locally before that is already done on the provider, and was
        // created before the provider was connected, is not pulled in.
        if (remote.IsDone && remote.CreatedAt < connectedAt)
        {
            return null;
        }

        TaskItem task = TaskItem.Create(remote.Title, remote.Description, remote.ExternalId, providerId, remote.CreatedAt);
        task.SetPriority(remote.Priority);
        task.SetProviderStatusName(remote.StatusName);
        await _taskRepository.AddAsync(task, cancellationToken);

        if (remote.IsInProgress || remote.IsDone)
        {
            task.Start();
            await _taskRepository.UpdateAsync(task, cancellationToken);
        }

        if (remote.IsDone)
        {
            task.Complete();
            await _taskRepository.UpdateAsync(task, cancellationToken);
        }

        return task;
    }

    /// <summary>
    /// Reacts to a status change detected on the provider for an already-known task, triggering
    /// the matching local transition (RF-015/CA-015.2/RN-011). A task already <c>Done</c> locally
    /// is immutable (RN-006) and is never touched here, regardless of what the provider reports.
    /// </summary>
    private async Task ApplyRemoteStatusTransitionAsync(TaskItem task, ProviderTaskSummary remote, CancellationToken cancellationToken)
    {
        if (task.Status == TaskStatus.Done)
        {
            return;
        }

        if (remote.IsDone)
        {
            if (task.Status is TaskStatus.InProgress or TaskStatus.Paused)
            {
                // Closes whichever session (active or pause) is currently open and completes the
                // task - EndWorkSessionUseCase doesn't care which WorkSessionType it is.
                await _endWorkSessionUseCase.ExecuteAsync(task.Id, cancellationToken);
            }
            else
            {
                var neverStarted = await _taskRepository.GetByIdAsync(task.Id, cancellationToken);
                if (neverStarted is not null)
                {
                    neverStarted.Start();
                    neverStarted.Complete();
                    await _taskRepository.UpdateAsync(neverStarted, cancellationToken);
                }
            }

            return;
        }

        if (remote.IsInProgress)
        {
            if (task.Status is TaskStatus.ToDo or TaskStatus.Paused)
            {
                await _startWorkSessionUseCase.ExecuteAsync(task.Id, cancellationToken);
            }

            return;
        }

        // RN-011: any status other than in-progress/done pauses tracking, no special-casing by
        // name - only applies once a task has actually been started (CA-015.2 mirrors CA-015.1).
        if (task.Status == TaskStatus.InProgress)
        {
            await _pauseWorkSessionUseCase.ExecuteAsync(task.Id, WorkSessionOrigin.Provider, cancellationToken);
        }
    }

    private async Task<DateTimeOffset> GetOrEstablishConnectedAtAsync(string providerId, CancellationToken cancellationToken)
    {
        var key = ProviderSettingsKeys.ConnectedAt(providerId);
        var stored = await _appSettingsStore.GetAsync(key, cancellationToken);
        if (stored is not null
            && DateTimeOffset.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed;
        }

        var connectedAt = DateTimeOffset.UtcNow;
        await _appSettingsStore.SetAsync(key, connectedAt.ToString("O", CultureInfo.InvariantCulture), cancellationToken);
        return connectedAt;
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
            Priority: task.Priority);
    }
}
