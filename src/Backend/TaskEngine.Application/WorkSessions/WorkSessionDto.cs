namespace TaskEngine.Application.WorkSessions;

/// <summary>
/// Output representation of a work session.
/// </summary>
public sealed record WorkSessionDto(Guid Id, Guid TaskId, DateTimeOffset StartedAt, DateTimeOffset? EndedAt);
