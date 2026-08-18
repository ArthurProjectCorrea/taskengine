using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;
using TaskEngine.Domain.Entities;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Desktop.Tests;

/// <summary>
/// Hand-written fake for <see cref="ITaskProviderClient"/>. Only <see cref="GetTaskSchemaAsync"/>
/// is exercised by <c>OnboardingViewModel</c>; <see cref="CreateTaskAsync"/>/<see cref="UpdateStatusAsync"/>
/// throw if called, since the onboarding flow has no business calling them.
/// </summary>
public sealed class FakeTaskProviderClient : ITaskProviderClient
{
    private readonly ProviderTaskSchema? _schema;
    private readonly Exception? _failure;

    public FakeTaskProviderClient(string providerId, ProviderTaskSchema schema)
    {
        ProviderId = providerId;
        _schema = schema;
    }

    public FakeTaskProviderClient(string providerId, Exception failure)
    {
        ProviderId = providerId;
        _failure = failure;
    }

    public string ProviderId { get; }

    public int GetTaskSchemaCallCount { get; private set; }

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
        TaskItem task, IReadOnlyDictionary<string, string>? fieldValues, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not used by the onboarding flow.");

    public Task UpdateStatusAsync(ProviderTaskReference reference, TaskStatus status, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not used by the onboarding flow.");
}
