namespace TaskEngine.Application.Providers;

/// <summary>
/// One task assigned to the connected provider's authenticated user, as returned by
/// <see cref="Abstractions.ITaskProviderClient.ListAssignedTasksAsync"/> (RF-001/Schema-001,
/// ERS-Tarefas.md). <see cref="IsInProgress"/>/<see cref="IsDone"/> are pre-classified by the
/// provider client (which owns the provider-specific mapping from raw status option names, same
/// as <see cref="Abstractions.ITaskProviderClient.UpdateStatusAsync"/>'s reverse direction), so
/// the Application layer never needs provider-specific string matching to apply RN-011 - any
/// status where both are false is uniformly "not in progress, not done", regardless of its name.
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
    string? Url);
