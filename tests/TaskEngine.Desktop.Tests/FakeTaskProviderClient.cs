using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Desktop.Tests;

/// <summary>
/// Hand-written fake for <see cref="ITaskProviderClient"/>. <see cref="UpdateStatusAsync"/> and
/// <see cref="ReportCompletionAsync"/> always throw - callers that need them (none today) should
/// extend this fake rather than leave a silently-succeeding no-op in place.
/// </summary>
public sealed class FakeTaskProviderClient : ITaskProviderClient
{
    private readonly IReadOnlyList<ProviderTaskSummary>? _assignedTasks;

    public FakeTaskProviderClient(string providerId, IReadOnlyList<ProviderTaskSummary>? assignedTasks = null)
    {
        ProviderId = providerId;
        _assignedTasks = assignedTasks;
    }

    public string ProviderId { get; }

    public Task UpdateStatusAsync(ProviderTaskReference reference, TaskStatus status, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not used by the onboarding flow.");

    /// <summary>Returns <see cref="_assignedTasks"/> when configured via the constructor's <c>assignedTasks</c> parameter - added for <c>ConfiguracoesViewModelTests</c>' reconnect flow (which ends in a <c>SyncTasksUseCase</c> pull); throws for callers that never configured it, same as this fake's original onboarding-only scope.</summary>
    public Task<IReadOnlyList<ProviderTaskSummary>> ListAssignedTasksAsync(CancellationToken cancellationToken) =>
        _assignedTasks is not null
            ? Task.FromResult(_assignedTasks)
            : throw new NotSupportedException("Not used by the onboarding flow.");

    public Task ReportCompletionAsync(ProviderTaskReference reference, TimeSpan totalDuration, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not used by the onboarding flow.");
}
