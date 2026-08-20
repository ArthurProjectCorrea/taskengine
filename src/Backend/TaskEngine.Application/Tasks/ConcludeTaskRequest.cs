namespace TaskEngine.Application.Tasks;

/// <summary>
/// Input for <see cref="ConcludeTaskUseCase"/>. <see cref="SelectedActivityIds"/> holds the
/// <c>ActivityInterval.Id</c>s the user marked as belonging to the task at conclusion time
/// (RF-007/Schema-003) - an empty set is valid input (CA-007.2: the "no time will be counted"
/// confirmation is a presentation-layer concern, not enforced here).
/// </summary>
public sealed record ConcludeTaskRequest(Guid TaskId, IReadOnlySet<Guid> SelectedActivityIds);
