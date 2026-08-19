namespace TaskEngine.Domain.Entities;

/// <summary>
/// Whether a <see cref="WorkSession"/> represents time actively counted toward a task
/// (<see cref="Active"/>) or a pause in tracking (<see cref="Pause"/>) — see RF-015/Schema-004
/// ("Período de Acompanhamento") in ERS-Tarefas.md.
/// </summary>
public enum WorkSessionType
{
    Active,
    Pause,
}
