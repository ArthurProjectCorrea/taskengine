namespace TaskEngine.Application.Tasks;

/// <summary>
/// Input for manually creating a task (no AI involved). When <see cref="ProviderId"/> is set, the
/// task is also created on that provider (e.g. "github") using <see cref="ProviderFieldValues"/>.
/// </summary>
public sealed record CreateTaskRequest(
    string Title,
    string? Description = null,
    string? ProviderId = null,
    IReadOnlyDictionary<string, string>? ProviderFieldValues = null);
