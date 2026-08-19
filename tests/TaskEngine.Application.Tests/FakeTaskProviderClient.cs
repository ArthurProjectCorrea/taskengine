using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;
using TaskEngine.Domain.Entities;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Application.Tests;

/// <summary>
/// Hand-written fake for <see cref="ITaskProviderClient"/>, no mocking library involved. Records
/// the arguments passed to <see cref="CreateTaskAsync"/> so tests can assert on them, and returns
/// a canned <see cref="ProviderTaskReference"/>.
/// </summary>
public sealed class FakeTaskProviderClient : ITaskProviderClient
{
    public string ProviderId { get; init; } = "github";

    public ProviderTaskReference ReferenceToReturn { get; set; } = new("github", "external-1", Url: null);

    public ProviderTaskSchema? SchemaToReturn { get; set; }

    public TaskItem? LastCreatedTask { get; private set; }

    public IReadOnlyDictionary<string, string>? LastFieldValues { get; private set; }

    public Task<ProviderTaskSchema> GetTaskSchemaAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(SchemaToReturn ?? new ProviderTaskSchema(ProviderId, []));
    }

    public Task<ProviderTaskReference> CreateTaskAsync(
        TaskItem task,
        IReadOnlyDictionary<string, string>? fieldValues,
        CancellationToken cancellationToken)
    {
        LastCreatedTask = task;
        LastFieldValues = fieldValues;
        return Task.FromResult(ReferenceToReturn);
    }

    public Task UpdateStatusAsync(ProviderTaskReference reference, TaskStatus status, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public IReadOnlyList<ProviderTaskSummary> AssignedTasksToReturn { get; set; } = [];

    public Task<IReadOnlyList<ProviderTaskSummary>> ListAssignedTasksAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(AssignedTasksToReturn);
    }
}
