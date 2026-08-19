using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.WorkSessions;

/// <summary>
/// Ends work on a task: transitions it to <c>Done</c> and closes its open <see cref="WorkSession"/>.
/// </summary>
public sealed class EndWorkSessionUseCase
{
    private readonly ITaskRepository _taskRepository;
    private readonly IWorkSessionRepository _workSessionRepository;

    public EndWorkSessionUseCase(ITaskRepository taskRepository, IWorkSessionRepository workSessionRepository)
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

        IReadOnlyList<WorkSession> sessions = await _workSessionRepository.ListByTaskIdAsync(taskId, cancellationToken);
        WorkSession? session = sessions.FirstOrDefault(s => s.IsOpen);
        if (session is null)
        {
            throw new InvalidOperationException($"Task '{taskId}' has no open work session.");
        }

        // Mutate in memory first (both calls can throw) so nothing is persisted if a validation fails.
        task.Complete();
        session.End(DateTimeOffset.UtcNow);

        await _taskRepository.UpdateAsync(task, cancellationToken);
        await _workSessionRepository.UpdateAsync(session, cancellationToken);

        return new WorkSessionDto(
            session.Id,
            session.TaskId,
            session.StartedAt,
            session.EndedAt,
            session.Type.ToString(),
            session.Origin.ToString());
    }
}
