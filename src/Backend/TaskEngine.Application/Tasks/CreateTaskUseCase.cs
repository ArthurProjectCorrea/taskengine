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

            if (request.ProviderFieldValues is { Count: > 0 })
            {
                ProviderTaskSchema schema = await providerClient.GetTaskSchemaAsync(cancellationToken);
                string? priority = ResolvePriority(schema, request.ProviderFieldValues);
                if (priority is not null)
                {
                    task.SetPriority(priority);
                }
            }

            await _taskRepository.UpdateAsync(task, cancellationToken);
        }

        return new TaskDto(
            task.Id,
            task.Title,
            task.Description,
            task.Status.ToString(),
            task.CreatedAt,
            ProviderTaskId: task.ProviderTaskId,
            Priority: task.Priority);
    }

    /// <summary>
    /// Finds the field the provider's dynamic schema (Schema-001 "Prioridade") labels "Priority"
    /// among <paramref name="fieldValues"/>, resolving its display label - for
    /// <see cref="ProviderFieldType.SingleSelect"/> fields <paramref name="fieldValues"/> holds
    /// the option id (per <see cref="ITaskProviderClient.CreateTaskAsync"/>), not the name shown
    /// to the user, so it must be looked up against <see cref="ProviderFieldDefinition.Options"/>.
    /// Returns null when the schema has no "Priority" field, or the caller didn't set a value for it.
    /// </summary>
    private static string? ResolvePriority(ProviderTaskSchema schema, IReadOnlyDictionary<string, string> fieldValues)
    {
        ProviderFieldDefinition? priorityField = schema.Fields.FirstOrDefault(
            f => string.Equals(f.Label, "Priority", StringComparison.OrdinalIgnoreCase));

        if (priorityField is null || !fieldValues.TryGetValue(priorityField.Key, out var rawValue))
        {
            return null;
        }

        if (priorityField.Type != ProviderFieldType.SingleSelect)
        {
            return rawValue;
        }

        return priorityField.Options.FirstOrDefault(o => o.Id == rawValue)?.Name ?? rawValue;
    }
}
