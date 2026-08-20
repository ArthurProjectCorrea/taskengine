using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Reports;

/// <summary>
/// One line of the per-task activity timeline report (RF-010/RF-011/Schema-003, ERS-Tarefas.md):
/// a single <see cref="ActivityInterval"/> that was marked as belonging to the task at conclusion
/// time. Unlike <see cref="TaskReportRow"/>, rows here are intentionally left unmerged - CA-010.1
/// requires overlaps between distinct items to stay visible instead of being folded away.
/// </summary>
public sealed record TaskActivityTimelineRow(
    Guid ActivityId,
    ActivityItemType? Type,
    string? Path,
    ActivitySource Source,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt)
{
    public TimeSpan Duration => EndedAt - StartedAt;
}
