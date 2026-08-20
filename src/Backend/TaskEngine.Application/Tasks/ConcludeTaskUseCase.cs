using TaskEngine.Application.Abstractions;
using TaskEngine.Domain.Entities;
using TaskEngine.Domain.TimeTracking;

namespace TaskEngine.Application.Tasks;

/// <summary>
/// Concludes a task with the user's selection of which activity items actually belong to it
/// (RF-007/Schema-003, ERS-Tarefas.md): applies the selection to every active
/// <see cref="WorkSession"/>, computes the final human/AI/total time strictly from the selected
/// items in active periods only (RN-005/RN-012 - pause periods, see <see cref="WorkSessionType.Pause"/>,
/// never contribute), and closes out tracking. <see cref="TimeIntervalMerger"/> (item #6) is used
/// twice: once per origin for <see cref="ConcludeTaskResult.HumanDuration"/>/
/// <see cref="ConcludeTaskResult.AiDuration"/>, and once across both origins together for
/// <see cref="ConcludeTaskResult.TotalDuration"/> (RN-007 - a period where human and AI activity
/// overlap is not double-counted in the total, even though it still counts once for each origin).
/// Provider sync (RF-009, "atualiza status + registra tempo trabalhado no GitHub") is added on top
/// of this same flow by the item that wires <see cref="ITaskProviderClient"/> in - not here.
/// </summary>
public sealed class ConcludeTaskUseCase
{
    private readonly ITaskRepository _taskRepository;
    private readonly IWorkSessionRepository _workSessionRepository;

    public ConcludeTaskUseCase(ITaskRepository taskRepository, IWorkSessionRepository workSessionRepository)
    {
        _taskRepository = taskRepository;
        _workSessionRepository = workSessionRepository;
    }

    public async Task<ConcludeTaskResult> ExecuteAsync(
        ConcludeTaskRequest request, CancellationToken cancellationToken = default)
    {
        TaskItem task = await _taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new InvalidOperationException($"Task '{request.TaskId}' was not found.");

        // Mutate the task first (throws if it isn't InProgress/Paused, e.g. already concluded -
        // RN-006) so nothing is persisted at all if the task itself can't be completed.
        task.Complete();

        IReadOnlyList<WorkSession> sessions = await _workSessionRepository.ListByTaskIdAsync(request.TaskId, cancellationToken);

        var humanIntervals = new List<TimeInterval>();
        var aiIntervals = new List<TimeInterval>();
        var allIntervals = new List<TimeInterval>();

        foreach (WorkSession session in sessions)
        {
            if (session.Type == WorkSessionType.Active)
            {
                session.ApplyActivitySelection(request.SelectedActivityIds);
                AccumulateSelectedIntervals(session, humanIntervals, aiIntervals, allIntervals);
            }

            // A task can be concluded while its current period is still open (in progress, or
            // paused without having been resumed first) - conclusion closes it out either way, so
            // no work session is left dangling open under a Done task.
            if (session.IsOpen)
            {
                session.End(DateTimeOffset.UtcNow);
            }

            await _workSessionRepository.UpdateAsync(session, cancellationToken);
        }

        await _taskRepository.UpdateAsync(task, cancellationToken);

        var humanDuration = TimeIntervalMerger.TotalDuration(humanIntervals);
        var aiDuration = TimeIntervalMerger.TotalDuration(aiIntervals);
        var totalDuration = TimeIntervalMerger.TotalDuration(allIntervals);

        return new ConcludeTaskResult(ToDto(task), humanDuration, aiDuration, totalDuration);
    }

    private static void AccumulateSelectedIntervals(
        WorkSession session, List<TimeInterval> humanIntervals, List<TimeInterval> aiIntervals, List<TimeInterval> allIntervals)
    {
        foreach (ActivityInterval activity in session.Activities)
        {
            if (!activity.SelectedAtConclusion)
            {
                continue;
            }

            var interval = new TimeInterval(activity.StartedAt, activity.EndedAt);
            allIntervals.Add(interval);

            List<TimeInterval> bucket = activity.Source == ActivitySource.Human ? humanIntervals : aiIntervals;
            bucket.Add(interval);
        }
    }

    private static TaskDto ToDto(TaskItem task)
    {
        return new TaskDto(
            task.Id,
            task.Title,
            task.Description,
            task.Status.ToString(),
            task.CreatedAt,
            ProviderTaskId: task.ProviderTaskId,
            Priority: task.Priority);
    }
}
