namespace TaskEngine.Application.Tasks;

/// <summary>
/// Input for manually creating a task (no AI involved).
/// </summary>
public sealed record CreateTaskRequest(string Title, string? Description = null);
