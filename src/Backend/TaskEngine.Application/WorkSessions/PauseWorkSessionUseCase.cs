using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.WorkSessions;

/// <summary>
/// Pauses time tracking for a task currently in progress (RF-015/CA-015.1, ERS-Tarefas.md):
/// closes the open active <see cref="WorkSession"/> and opens a new pause session in its place.
/// <paramref name="origin"/> defaults to <see cref="WorkSessionOrigin.System"/> for a direct user
/// action; the sync use case passes <see cref="WorkSessionOrigin.Provider"/> when a status change
/// away from "in progress"/"done" is detected on the provider instead (CA-015.2).
/// </summary>
public sealed class PauseWorkSessionUseCase
{
    private readonly ITaskRepository _taskRepository;
    private readonly IWorkSessionRepository _workSessionRepository;

    public PauseWorkSessionUseCase(ITaskRepository taskRepository, IWorkSessionRepository workSessionRepository)
    {
        _taskRepository = taskRepository;
        _workSessionRepository = workSessionRepository;
    }

    public async Task<WorkSessionDto> ExecuteAsync(
        Guid taskId,
        WorkSessionOrigin origin = WorkSessionOrigin.System,
        CancellationToken cancellationToken = default)
    {
        TaskItem? task = await _taskRepository.GetByIdAsync(taskId, cancellationToken);
        if (task is null)
        {
            throw new InvalidOperationException($"Task '{taskId}' was not found.");
        }

        task.Pause();
        await _taskRepository.UpdateAsync(task, cancellationToken);

        IReadOnlyList<WorkSession> sessions = await _workSessionRepository.ListByTaskIdAsync(taskId, cancellationToken);
        WorkSession? openActive = sessions.FirstOrDefault(s => s.IsOpen && s.Type == WorkSessionType.Active);
        if (openActive is not null)
        {
            openActive.End(DateTimeOffset.UtcNow);
            await _workSessionRepository.UpdateAsync(openActive, cancellationToken);
        }

        WorkSession pauseSession = WorkSession.Start(taskId, DateTimeOffset.UtcNow, WorkSessionType.Pause, origin);
        await _workSessionRepository.AddAsync(pauseSession, cancellationToken);

        return new WorkSessionDto(
            pauseSession.Id,
            pauseSession.TaskId,
            pauseSession.StartedAt,
            pauseSession.EndedAt,
            pauseSession.Type.ToString(),
            pauseSession.Origin.ToString());
    }
}
