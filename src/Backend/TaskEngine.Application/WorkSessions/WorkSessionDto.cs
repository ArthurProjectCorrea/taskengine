namespace TaskEngine.Application.WorkSessions;

/// <summary>
/// Output representation of a work session. Uses <c>string</c> for <see cref="Type"/> and
/// <see cref="Origin"/> so the Domain's <c>WorkSessionType</c>/<c>WorkSessionOrigin</c> enums
/// never leak past the Application layer (same rationale as <c>TaskDto.Status</c>).
/// </summary>
public sealed record WorkSessionDto(
    Guid Id,
    Guid TaskId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    string Type = "Active",
    string Origin = "System");
