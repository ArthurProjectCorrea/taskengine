namespace TaskEngine.Application.UnmappedTime;

public sealed record UnmappedTimeEntryDto(
    Guid Id,
    Guid TaskId,
    TimeSpan Duration,
    string Justification,
    DateTimeOffset RecordedAt);
