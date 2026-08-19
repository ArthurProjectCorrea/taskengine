namespace TaskEngine.Domain.Entities;

/// <summary>
/// Nature of the item an <see cref="ActivityInterval"/> represents — a local file edited, or a
/// browser page visited. See Schema-003 ("Item de Atividade") in ERS-Tarefas.md/ERS-Monitoramento.md.
/// </summary>
public enum ActivityItemType
{
    File,
    Browser,
}
