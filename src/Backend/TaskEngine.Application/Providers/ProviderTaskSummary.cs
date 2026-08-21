namespace TaskEngine.Application.Providers;

/// <summary>
/// One task assigned to the connected provider's authenticated user, as returned by
/// <see cref="Abstractions.ITaskProviderClient.ListAssignedTasksAsync"/> (RF-001/Schema-001,
/// ERS-Tarefas.md). <see cref="IsInProgress"/>/<see cref="IsDone"/> are pre-classified by the
/// provider client (which owns the provider-specific mapping from raw status option names, same
/// as <see cref="Abstractions.ITaskProviderClient.UpdateStatusAsync"/>'s reverse direction), so
/// the Application layer never needs provider-specific string matching to apply RN-011 - any
/// status where both are false is uniformly "not in progress, not done", regardless of its name.
/// <para/>
/// <see cref="HasRecognizedProjectStatus"/> distinguishes two different reasons
/// <see cref="IsInProgress"/> can be false: (a) the provider genuinely has no status signal at all
/// (e.g. a plain GitHub Issue with no linked Projects v2 board - default <see langword="false"/>,
/// preserving prior behavior), vs. (b) the provider positively confirms the task is tracked
/// somewhere (e.g. on a Projects v2 board with a recognized Status field) but that status simply
/// isn't "in progress" (e.g. moved back to "A Fazer"/"Backlog"). Only case (b) is a trustworthy
/// enough signal for <c>SyncTasksUseCase.ApplyRemoteStatusTransitionAsync</c> to auto-pause local
/// time tracking (RN-011); case (a) must not, since "no information" is not the same as "actively
/// not in progress".
/// </summary>
public sealed record ProviderTaskSummary(
    string ExternalId,
    string Title,
    string? Description,
    string? StatusName,
    bool IsInProgress,
    bool IsDone,
    DateTimeOffset CreatedAt,
    string? Priority,
    string? Url,
    bool HasRecognizedProjectStatus = false);
