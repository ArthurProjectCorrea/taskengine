namespace TaskEngine.Desktop.ViewModels.Navigation;

/// <summary>
/// The top-level areas reachable from within the shell. <see cref="Dashboard"/>/<see cref="Tarefas"/>/
/// <see cref="Configuracoes"/> match the three sidebar icons in the approved prototype (issue #2,
/// see docs/prototipo/*.dc.html) - <see cref="ShellViewModel"/> exposes one navigation command per
/// icon for exactly those three. <see cref="DetalhesTarefa"/> (issue #53) is reachable only by
/// navigating "on top of" Tarefas with a task id parameter (see <see cref="INavigationService"/>),
/// not from a sidebar icon of its own - it does not imply every enum value maps to a sidebar entry.
/// </summary>
public enum AppSection
{
    Dashboard,
    Tarefas,
    Configuracoes,

    /// <summary>
    /// Details of a single task (issue #53, ERS-Tarefas.md) - reached via
    /// <see cref="INavigationService.NavigateTo(AppSection, object?)"/> with the task's
    /// <see cref="Guid"/> id as the parameter. Deliberately not a sidebar-level section: it is
    /// always opened from Tarefas (or, in the future, other task lists) rather than picked
    /// directly by the user from the shell, so <see cref="ShellViewModel"/> has no command for it.
    /// </summary>
    DetalhesTarefa,

    /// <summary>
    /// Per-task activity timeline report (plan item 9, RF-010/RF-011, ERS-Tarefas.md) - reached
    /// via <see cref="INavigationService.NavigateTo(AppSection, object?)"/> with the task's
    /// <see cref="Guid"/> id as the parameter, same convention as <see cref="DetalhesTarefa"/>.
    /// Only ever opened from <see cref="DetalhesTarefa"/>'s "Relatório" action (gated there on the
    /// task being concluded) - not a sidebar-level section either, so <see cref="ShellViewModel"/>
    /// has no command for it.
    /// </summary>
    Relatorio,
}
