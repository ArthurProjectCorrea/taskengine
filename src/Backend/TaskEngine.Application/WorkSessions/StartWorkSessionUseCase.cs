using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Application.WorkSessions;

/// <summary>
/// Starts work on a task: transitions it from <c>ToDo</c> to <c>InProgress</c> and opens a new
/// active <see cref="WorkSession"/>. Also handles the RF-015 "retomar" (resume) case: a
/// <c>Paused</c> task closes its open pause session and reopens an active one, transitioning back
/// to <c>InProgress</c> (CA-015.3).
/// </summary>
public sealed class StartWorkSessionUseCase
{
    private readonly ITaskRepository _taskRepository;
    private readonly IWorkSessionRepository _workSessionRepository;

    public StartWorkSessionUseCase(ITaskRepository taskRepository, IWorkSessionRepository workSessionRepository)
    {
        _taskRepository = taskRepository;
        _workSessionRepository = workSessionRepository;
    }

    public async Task<WorkSessionDto> ExecuteAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        TaskItem? task = await _taskRepository.GetByIdAsync(taskId, cancellationToken);
        if (task is null)
        {
            throw new InvalidOperationException($"Task '{taskId}' was not found.");
        }

        if (task.Status == TaskStatus.Paused)
        {
            return await ResumeAsync(task, cancellationToken);
        }

        task.Start();
        await _taskRepository.UpdateAsync(task, cancellationToken);

        WorkSession session = WorkSession.Start(taskId, DateTimeOffset.UtcNow);
        await _workSessionRepository.AddAsync(session, cancellationToken);

        return ToDto(session);
    }

    private async Task<WorkSessionDto> ResumeAsync(TaskItem task, CancellationToken cancellationToken)
    {
        task.Resume();
        await _taskRepository.UpdateAsync(task, cancellationToken);

        IReadOnlyList<WorkSession> sessions = await _workSessionRepository.ListByTaskIdAsync(task.Id, cancellationToken);
        WorkSession? openPause = sessions.FirstOrDefault(s => s.IsOpen && s.Type == WorkSessionType.Pause);
        if (openPause is not null)
        {
            openPause.End(DateTimeOffset.UtcNow);
            await _workSessionRepository.UpdateAsync(openPause, cancellationToken);
        }

        WorkSession session = WorkSession.Start(task.Id, DateTimeOffset.UtcNow);
        await _workSessionRepository.AddAsync(session, cancellationToken);

        return ToDto(session);
    }

    private static WorkSessionDto ToDto(WorkSession session)
    {
        return new WorkSessionDto(
            session.Id,
            session.TaskId,
            session.StartedAt,
            session.EndedAt,
            session.Type.ToString(),
            session.Origin.ToString());
    }
}
