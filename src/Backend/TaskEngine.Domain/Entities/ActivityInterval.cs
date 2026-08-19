namespace TaskEngine.Domain.Entities;

/// <summary>
/// Immutable slice of activity within a <see cref="WorkSession"/>, attributed to either a
/// human or an AI agent - the domain representation of Schema-003 ("Item de Atividade",
/// ERS-Tarefas.md/ERS-Monitoramento.md). <see cref="Id"/> gives each interval a stable identity
/// so a specific item can be referenced later - e.g. when the user selects which activities
/// actually belong to a task at conclusion time (RF-007), recorded via
/// <see cref="SelectedAtConclusion"/>. <see cref="Type"/>/<see cref="Path"/> are optional because
/// not every recorded interval necessarily originates from a monitored file/browser event.
/// </summary>
public sealed record ActivityInterval
{
    public Guid Id { get; }
    public ActivitySource Source { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset EndedAt { get; }
    public ActivityItemType? Type { get; }
    public string? Path { get; }
    public bool SelectedAtConclusion { get; init; }

    public ActivityInterval(
        ActivitySource source,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        ActivityItemType? type = null,
        string? path = null,
        bool selectedAtConclusion = false,
        Guid? id = null)
    {
        if (endedAt <= startedAt)
        {
            throw new ArgumentException("EndedAt must be after StartedAt.", nameof(endedAt));
        }

        Id = id ?? Guid.NewGuid();
        Source = source;
        StartedAt = startedAt;
        EndedAt = endedAt;
        Type = type;
        Path = path;
        SelectedAtConclusion = selectedAtConclusion;
    }

    public TimeSpan Duration => EndedAt - StartedAt;
}
