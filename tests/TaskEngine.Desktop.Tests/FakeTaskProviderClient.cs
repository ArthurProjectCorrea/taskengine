using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;
using TaskEngine.Domain.Entities;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Desktop.Tests;

/// <summary>
/// Hand-written fake for <see cref="ITaskProviderClient"/>. Originally (issue #20) only exercised
/// <see cref="GetTaskSchemaAsync"/>; issue #26 reuses it for <c>CreateTaskViewModel</c> tests too,
/// so <see cref="CreateTaskAsync"/> is now configurable via <paramref name="createTaskResult"/> -
/// callers that don't pass one (e.g. the pre-existing onboarding tests) keep the old "throws if
/// called" behavior. <see cref="UpdateStatusAsync"/> is still unused by either flow.
/// </summary>
public sealed class FakeTaskProviderClient : ITaskProviderClient
{
    private readonly ProviderTaskSchema? _schema;
    private readonly Exception? _failure;
    private readonly ProviderTaskReference? _createTaskResult;

    public FakeTaskProviderClient(string providerId, ProviderTaskSchema schema, ProviderTaskReference? createTaskResult = null)
    {
        ProviderId = providerId;
        _schema = schema;
        _createTaskResult = createTaskResult;
    }

    public FakeTaskProviderClient(string providerId, Exception failure)
    {
        ProviderId = providerId;
        _failure = failure;
    }

    public string ProviderId { get; }

    public int GetTaskSchemaCallCount { get; private set; }

    public int CreateTaskCallCount { get; private set; }

    /// <summary>Captures what was actually sent to <see cref="CreateTaskAsync"/>, so tests can assert e.g. that a SingleSelect field's value is the option's Id, not its display Name.</summary>
    public IReadOnlyDictionary<string, string>? LastCreateTaskFieldValues { get; private set; }

    public Task<ProviderTaskSchema> GetTaskSchemaAsync(CancellationToken cancellationToken)
    {
        GetTaskSchemaCallCount++;

        if (_failure is not null)
        {
            throw _failure;
        }

        return Task.FromResult(_schema!);
    }

    public Task<ProviderTaskReference> CreateTaskAsync(
        TaskItem task, IReadOnlyDictionary<string, string>? fieldValues, CancellationToken cancellationToken)
    {
        CreateTaskCallCount++;
        LastCreateTaskFieldValues = fieldValues;

        if (_failure is not null)
        {
            throw _failure;
        }

        if (_createTaskResult is null)
        {
            throw new NotSupportedException("This fake was not configured with a CreateTaskAsync result.");
        }

        return Task.FromResult(_createTaskResult);
    }

    public Task UpdateStatusAsync(ProviderTaskReference reference, TaskStatus status, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not used by the onboarding or task-creation flows.");

    public Task<IReadOnlyList<ProviderTaskSummary>> ListAssignedTasksAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not used by the onboarding or task-creation flows.");
}
