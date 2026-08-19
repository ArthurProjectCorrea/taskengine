namespace TaskEngine.Desktop.ViewModels.Navigation;

/// <summary>
/// The three top-level areas reachable from the shell's sidebar (issue #2), matching the
/// Dashboard/Tarefas/Configurações icons in the approved prototype (see
/// docs/prototipo/*.dc.html). Only <see cref="ShellViewModel"/> and <see cref="INavigationService"/>
/// know about this today - it does not imply the real screens exist yet (that is Fase E); until
/// then every section resolves to a placeholder view.
/// </summary>
public enum AppSection
{
    Dashboard,
    Tarefas,
    Configuracoes,
}
