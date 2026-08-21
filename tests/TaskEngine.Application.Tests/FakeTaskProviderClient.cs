using TaskEngine.Application.Abstractions;
using TaskEngine.Application.Providers;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Application.Tests;

/// <summary>
/// Hand-written fake for <see cref="ITaskProviderClient"/>, no mocking library involved.
/// </summary>
public sealed class FakeTaskProviderClient : ITaskProviderClient
{
    public string ProviderId { get; init; } = "github";

    public IReadOnlyList<ProviderTaskSummary> AssignedTasksToReturn { get; set; } = [];

    public Task<IReadOnlyList<ProviderTaskSummary>> ListAssignedTasksAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(AssignedTasksToReturn);
    }

    public ProviderTaskReference? LastUpdatedStatusReference { get; private set; }

    public TaskStatus? LastUpdatedStatus { get; private set; }

    public Task UpdateStatusAsync(ProviderTaskReference reference, TaskStatus status, CancellationToken cancellationToken)
    {
        LastUpdatedStatusReference = reference;
        LastUpdatedStatus = status;
        return Task.CompletedTask;
    }

    public ProviderTaskReference? LastReportedCompletionReference { get; private set; }

    public TimeSpan? LastReportedCompletionDuration { get; private set; }

    public Exception? ReportCompletionFailure { get; set; }

    public Task ReportCompletionAsync(ProviderTaskReference reference, TimeSpan totalDuration, CancellationToken cancellationToken)
    {
        if (ReportCompletionFailure is not null)
        {
            throw ReportCompletionFailure;
        }

        LastReportedCompletionReference = reference;
        LastReportedCompletionDuration = totalDuration;
        return Task.CompletedTask;
    }
}
