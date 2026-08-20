namespace TaskEngine.Application.Tasks;

/// <summary>
/// Output of <see cref="ConcludeTaskUseCase"/>. <see cref="TotalDuration"/> is not necessarily
/// <see cref="HumanDuration"/> + <see cref="AiDuration"/> - RN-007 (ERS-Tarefas.md) requires any
/// overlap between human and AI activity to count once toward the total, even though it still
/// counts fully toward each origin's own duration.
/// </summary>
public sealed record ConcludeTaskResult(TaskDto Task, TimeSpan HumanDuration, TimeSpan AiDuration, TimeSpan TotalDuration);
