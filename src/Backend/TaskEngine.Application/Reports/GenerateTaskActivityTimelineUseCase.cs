using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;

using TaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Application.Reports;

/// <summary>
/// Builds the per-task activity timeline report (RF-010/RF-011/Schema-003, ERS-Tarefas.md): one
/// <see cref="TaskActivityTimelineRow"/> per activity item selected at conclusion time (RF-007),
/// ordered chronologically by start. Deliberately does not merge overlapping intervals like
/// <see cref="GenerateTaskReportUseCase"/> does - CA-010.1 requires each item's own period to stay
/// visible even when it overlaps another item's. Only applies to completed tasks (RF-010's own
/// wording: "Para tarefas concluídas") - <see cref="TaskStatus.Done"/> and
/// <see cref="TaskStatus.DonePendingSync"/> both qualify, since RF-014 already treats the latter as
/// locally final (RN-006), just not yet pushed to the provider. Formatting is a separate concern -
/// see <see cref="CsvTaskActivityTimelineWriter"/>.
/// </summary>
public sealed class GenerateTaskActivityTimelineUseCase
{
    private readonly ITaskRepository _taskRepository;
    private readonly IWorkSessionRepository _workSessionRepository;

    public GenerateTaskActivityTimelineUseCase(ITaskRepository taskRepository, IWorkSessionRepository workSessionRepository)
    {
        _taskRepository = taskRepository;
        _workSessionRepository = workSessionRepository;
    }

    public async Task<IReadOnlyList<TaskActivityTimelineRow>> ExecuteAsync(
        Guid taskId, CancellationToken cancellationToken = default)
    {
        TaskItem task = await _taskRepository.GetByIdAsync(taskId, cancellationToken)
            ?? throw new InvalidOperationException($"Task '{taskId}' was not found.");

        if (task.Status != TaskStatus.Done && task.Status != TaskStatus.DonePendingSync)
        {
            throw new InvalidOperationException(
                $"Cannot generate the activity timeline for a task in status '{task.Status}' - RF-010 applies to completed tasks only.");
        }

        IReadOnlyList<WorkSession> sessions = await _workSessionRepository.ListByTaskIdAsync(taskId, cancellationToken);

        var rows = new List<TaskActivityTimelineRow>();
        foreach (WorkSession session in sessions)
        {
            foreach (ActivityInterval activity in session.Activities)
            {
                if (!activity.SelectedAtConclusion)
                {
                    continue;
                }

                rows.Add(new TaskActivityTimelineRow(
                    activity.Id, activity.Type, activity.Path, activity.Source, activity.StartedAt, activity.EndedAt));
            }
        }

        return rows.OrderBy(row => row.StartedAt).ToList();
    }
}
