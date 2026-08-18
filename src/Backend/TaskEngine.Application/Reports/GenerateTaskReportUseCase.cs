using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

namespace TaskEngine.Application.Reports;

/// <summary>
/// Builds the consolidated task report (phase 1 of #38): one <see cref="TaskReportRow"/> per
/// task, aggregating its <see cref="WorkSession"/> data. Formatting (CSV or otherwise) is a
/// separate concern - see <see cref="CsvTaskReportWriter"/>.
/// </summary>
public sealed class GenerateTaskReportUseCase
{
    private readonly ITaskRepository _taskRepository;
    private readonly IWorkSessionRepository _workSessionRepository;

    public GenerateTaskReportUseCase(ITaskRepository taskRepository, IWorkSessionRepository workSessionRepository)
    {
        _taskRepository = taskRepository;
        _workSessionRepository = workSessionRepository;
    }

    public async Task<IReadOnlyList<TaskReportRow>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TaskItem> tasks = await _taskRepository.ListAsync(cancellationToken);
        var rows = new List<TaskReportRow>(tasks.Count);

        foreach (TaskItem task in tasks)
        {
            IReadOnlyList<WorkSession> sessions = await _workSessionRepository.ListByTaskIdAsync(task.Id, cancellationToken);
            rows.Add(BuildRow(task, sessions));
        }

        return rows;
    }

    private static TaskReportRow BuildRow(TaskItem task, IReadOnlyList<WorkSession> sessions)
    {
        DateOnly? startedAt = null;
        DateOnly? endedAt = null;
        var aiSeconds = 0d;
        var humanSeconds = 0d;

        foreach (WorkSession session in sessions)
        {
            var sessionStartedAt = DateOnly.FromDateTime(session.StartedAt.UtcDateTime);
            if (startedAt is null || sessionStartedAt < startedAt)
            {
                startedAt = sessionStartedAt;
            }

            if (session.EndedAt is { } sessionEndedAt)
            {
                var sessionEndedOn = DateOnly.FromDateTime(sessionEndedAt.UtcDateTime);
                if (endedAt is null || sessionEndedOn > endedAt)
                {
                    endedAt = sessionEndedOn;
                }
            }

            aiSeconds += session.AiDuration.TotalSeconds;
            humanSeconds += session.HumanDuration.TotalSeconds;
        }

        return new TaskReportRow(task.Id, startedAt, endedAt, task.ProviderId, aiSeconds, humanSeconds);
    }
}
