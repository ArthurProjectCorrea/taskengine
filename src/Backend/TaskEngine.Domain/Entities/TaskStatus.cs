namespace TaskEngine.Domain.Entities;

/// <summary>
/// Derived classification of a <see cref="TaskItem"/>'s status. Per RN-010/RN-011
/// (ERS-Tarefas.md), the actual status *options* are defined by the connected provider and are
/// not fixed in the system — the raw provider label is kept separately in
/// <see cref="TaskItem.ProviderStatusName"/>. This enum only captures the handful of
/// classifications the system's own rules care about: any provider status other than
/// "in progress"/"done" is treated uniformly as <see cref="Paused"/>, with no special-casing by
/// name (e.g. "cancelled" is just another pause).
/// </summary>
public enum TaskStatus
{
    ToDo,
    InProgress,
    Paused,
    Done,
}
