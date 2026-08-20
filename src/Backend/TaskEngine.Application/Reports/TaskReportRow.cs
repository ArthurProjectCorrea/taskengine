namespace TaskEngine.Application.Reports;

/// <summary>
/// One consolidated line of the general task report (RF-016/Schema-005, ERS-Tarefas.md):
/// already-computed data, no formatting applied yet (see <see cref="CsvTaskReportWriter"/> for
/// that). <see cref="StartedAt"/> is the earliest <c>WorkSession.StartedAt</c> across the task's
/// sessions (<c>null</c> if it has none); <see cref="EndedAt"/> is the latest
/// <c>WorkSession.EndedAt</c> among only the sessions that are already closed (<c>null</c> if none
/// are closed yet, even if the task has open sessions). <see cref="AiSeconds"/>/
/// <see cref="HumanSeconds"/>/<see cref="TotalSeconds"/> are all computed via
/// <c>TimeIntervalMerger</c> over active (non-paused) sessions only, so overlapping activity
/// within the same origin - and, for <see cref="TotalSeconds"/>, across both origins (RN-007) -
/// is never double-counted. <see cref="ScheduledSeconds"/> is the expected work-schedule time over
/// [<see cref="StartedAt"/>, <see cref="EndedAt"/>] (zero when either is unknown, or no schedule is
/// configured). <see cref="UnmappedSeconds"/>/<see cref="UnmappedJustifications"/> come directly
/// from the task's recorded <c>UnmappedTimeEntry</c> rows.
/// </summary>
public sealed record TaskReportRow(
    Guid TaskId,
    DateOnly? StartedAt,
    DateOnly? EndedAt,
    string? ProviderId,
    double AiSeconds,
    double HumanSeconds,
    double ScheduledSeconds,
    double UnmappedSeconds,
    IReadOnlyList<string> UnmappedJustifications,
    double TotalSeconds);
