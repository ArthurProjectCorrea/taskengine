using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Tasks;

/// <summary>
/// Creates a task manually (no AI involved) and persists it locally.
/// </summary>
public sealed class CreateTaskUseCase
{
    private readonly ITaskRepository _taskRepository;
    private readonly IProviderClientFactory _providerClientFactory;

    public CreateTaskUseCase(ITaskRepository taskRepository, IProviderClientFactory providerClientFactory)
    {
        _taskRepository = taskRepository;
        _providerClientFactory = providerClientFactory;
    }

    public async Task<TaskDto> ExecuteAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        TaskItem task = TaskItem.Create(request.Title, request.Description);

        await _taskRepository.AddAsync(task, cancellationToken);

        if (request.ProviderId is not null)
        {
            ITaskProviderClient providerClient = await _providerClientFactory.CreateAsync(request.ProviderId, cancellationToken);
            ProviderTaskReference reference = await providerClient.CreateTaskAsync(task, request.ProviderFieldValues, cancellationToken);

            task.AttachProviderReference(request.ProviderId, reference.ExternalId);
            await _taskRepository.UpdateAsync(task, cancellationToken);
        }

        return new TaskDto(
            task.Id,
            task.Title,
            task.Description,
            task.Status.ToString(),
            task.CreatedAt,
            ProviderTaskId: task.ProviderTaskId);
    }
}
