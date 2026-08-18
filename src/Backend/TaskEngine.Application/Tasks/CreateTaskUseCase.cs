using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Tasks;

/// <summary>
/// Creates a task manually (no AI involved) and persists it locally.
/// </summary>
public sealed class CreateTaskUseCase
{
    private readonly ITaskRepository _taskRepository;

    public CreateTaskUseCase(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<TaskDto> ExecuteAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        TaskItem task = TaskItem.Create(request.Title, request.Description);

        await _taskRepository.AddAsync(task, cancellationToken);

        return new TaskDto(task.Id, task.Title, task.Description, task.Status.ToString(), task.CreatedAt);
    }
}
