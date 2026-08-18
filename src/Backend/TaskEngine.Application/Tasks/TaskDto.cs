namespace TaskEngine.Application.Tasks;

/// <summary>
/// Output representation of a task. Uses <c>string</c> for <see cref="Status"/> so the
/// Domain's <c>TaskStatus</c> enum never leaks past the Application layer.
/// </summary>
public sealed record TaskDto(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    DateTimeOffset CreatedAt,
    string? ProviderTaskId = null);
