using DomainTaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Desktop.ViewModels;

/// <summary>
/// Pure, UI-framework-agnostic rules for which <see cref="DomainTaskStatus"/> transitions the UI
/// may offer the user, and how to label the status/actions. Backs <c>StatusButtonView</c> and is
/// meant to be reused by every screen that renders/acts on a task status (Dashboard, Tarefas,
/// Detalhes - Fase E) so the set of valid transitions is defined in exactly one place instead of
/// being duplicated per screen.
///
/// Deliberately says nothing about *what happens* when a transition is picked - it only says
/// which transitions are legal and how to label them. Callers (view models) decide the actual
/// effect (e.g. invoking <c>StartWorkSessionUseCase</c>/<c>ConcludeTaskUseCase</c>); this class
/// has no dependency on <c>TaskEngine.Application</c> use cases on purpose, so it stays trivially
/// testable without any fakes/mocks.
/// </summary>
public static class StatusTransitionRules
{
    /// <summary>
    /// UI-permitted transitions away from <paramref name="currentStatus"/>.
    /// <see cref="DomainTaskStatus.Done"/> and <see cref="DomainTaskStatus.DonePendingSync"/> are
    /// terminal from the UI's perspective (RN-006) - no transitions are offered for either, even
    /// though the domain models a DonePendingSync -&gt; Done transition, because that one is
    /// driven by background sync succeeding, not by a user picking it from a menu.
    /// </summary>
    public static IReadOnlyList<DomainTaskStatus> GetValidTransitions(DomainTaskStatus currentStatus) => currentStatus switch
    {
        DomainTaskStatus.ToDo => [DomainTaskStatus.InProgress],
        DomainTaskStatus.InProgress => [DomainTaskStatus.Paused, DomainTaskStatus.Done],
        DomainTaskStatus.Paused => [DomainTaskStatus.InProgress],
        DomainTaskStatus.Done => [],
        DomainTaskStatus.DonePendingSync => [],
        _ => [],
    };

    /// <summary>Display label for the status itself (e.g. the status button's own text).</summary>
    public static string GetStatusLabel(DomainTaskStatus status) => status switch
    {
        DomainTaskStatus.ToDo => "A Fazer",
        DomainTaskStatus.InProgress => "Em Andamento",
        DomainTaskStatus.Paused => "Pausado",
        DomainTaskStatus.Done => "Concluído",
        DomainTaskStatus.DonePendingSync => "Concluído (sincronização pendente)",
        _ => status.ToString(),
    };

    /// <summary>
    /// Label for the dropdown option that performs the <paramref name="from"/> -&gt;
    /// <paramref name="to"/> transition (e.g. "Iniciar", "Pausar") - an action verb, not the
    /// destination status's own name.
    /// </summary>
    public static string GetTransitionActionLabel(DomainTaskStatus from, DomainTaskStatus to) => (from, to) switch
    {
        (DomainTaskStatus.ToDo, DomainTaskStatus.InProgress) => "Iniciar",
        (DomainTaskStatus.InProgress, DomainTaskStatus.Paused) => "Pausar",
        (DomainTaskStatus.InProgress, DomainTaskStatus.Done) => "Concluir",
        (DomainTaskStatus.Paused, DomainTaskStatus.InProgress) => "Retomar",
        _ => GetStatusLabel(to),
    };
}
