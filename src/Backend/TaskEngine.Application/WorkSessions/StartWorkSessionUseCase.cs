using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.WorkSessions;

/// <summary>
/// Starts work on a task: transitions it to <c>InProgress</c> and opens a new <see cref="WorkSession"/>.
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

        task.Start();
        await _taskRepository.UpdateAsync(task, cancellationToken);

        WorkSession session = WorkSession.Start(taskId, DateTimeOffset.UtcNow);
        await _workSessionRepository.AddAsync(session, cancellationToken);

        return new WorkSessionDto(session.Id, session.TaskId, session.StartedAt, session.EndedAt);
    }
}
