namespace TaskEngine.Application.Reports;

/// <summary>
/// One consolidated line of the task report: already-computed data, no formatting applied yet
/// (see <see cref="CsvTaskReportWriter"/> for that). <see cref="StartedAt"/> is the earliest
/// <c>WorkSession.StartedAt</c> across the task's sessions (<c>null</c> if it has none);
/// <see cref="EndedAt"/> is the latest <c>WorkSession.EndedAt</c> among only the sessions that
/// are already closed (<c>null</c> if none are closed yet, even if the task has open sessions).
/// </summary>
public sealed record TaskReportRow(
    Guid TaskId,
    DateOnly? StartedAt,
    DateOnly? EndedAt,
    string? ProviderId,
    double AiSeconds,
    double HumanSeconds);
