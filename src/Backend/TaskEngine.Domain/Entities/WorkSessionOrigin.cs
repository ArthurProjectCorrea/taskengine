namespace TaskEngine.Domain.Entities;

/// <summary>
/// Where the status change that opened a <see cref="WorkSession"/> came from — the user acting
/// directly in the system (<see cref="System"/>), or a status change detected while syncing with
/// the external provider (<see cref="Provider"/>). See RF-015/Schema-004 in ERS-Tarefas.md.
/// </summary>
public enum WorkSessionOrigin
{
    System,
    Provider,
}
