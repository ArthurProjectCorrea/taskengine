using TaskEngine.Application.Providers;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Application.Abstractions;

/// <summary>
/// Port for a task provider (e.g. GitHub, Jira, ClickUp) capable of listing a user's assigned
/// tasks and synchronizing their status. Implemented by TaskEngine.Infrastructure.
/// </summary>
public interface ITaskProviderClient
{
    string ProviderId { get; }

    Task UpdateStatusAsync(ProviderTaskReference reference, TaskStatus status, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the provider's tasks currently assigned to the authenticated user (RF-001), for
    /// <c>SyncTasksUseCase</c> to pull in and upsert locally. This is the primary path for
    /// getting tasks into the system in this version of the ERS - the user works from tasks that
    /// already exist on the provider, rather than creating them from TaskEngine.
    /// </summary>
    Task<IReadOnlyList<ProviderTaskSummary>> ListAssignedTasksAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Pushes the final outcome of a locally concluded task to the provider (RF-009): updates its
    /// status to done and records <paramref name="totalDuration"/> in a way the specific
    /// implementation supports (e.g. GitHub Issues has no custom time field, so
    /// <c>GitHubIssuesClient</c> posts it as a comment instead). Used by <c>ConcludeTaskUseCase</c>.
    /// </summary>
    Task ReportCompletionAsync(ProviderTaskReference reference, TimeSpan totalDuration, CancellationToken cancellationToken);
}
