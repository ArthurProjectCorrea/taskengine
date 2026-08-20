namespace TaskEngine.Desktop.ViewModels.Navigation;

/// <summary>
/// In-memory navigation between the shell's sections (issue #2) - no page stack, no URI routing:
/// the shell is a single window with a persistent sidebar, and "navigating" just means changing
/// which section's view is displayed in the content area. Deliberately MAUI-free (no
/// <c>Microsoft.Maui.Controls</c> types in this contract) so <see cref="ShellViewModel"/> can
/// depend on it and stay testable outside a MAUI host; the concrete implementation (which also
/// knows how to resolve an actual View per section) lives in TaskEngine.Desktop.
/// </summary>
public interface INavigationService
{
    AppSection CurrentSection { get; }

    /// <summary>
    /// The parameter passed to the most recent <see cref="NavigateTo"/> call (issue #53) - e.g.
    /// the <see cref="Guid"/> task id for <see cref="AppSection.DetalhesTarefa"/>. Null for
    /// sections that don't take one (Dashboard/Tarefas/Configuracoes today). Read by the
    /// Desktop-only <c>SectionViewFactory</c> after resolving the section's View, to hand it to
    /// the resolved view model via <see cref="INavigationAware"/> if it opts in.
    /// </summary>
    object? CurrentParameter { get; }

    event Action<AppSection>? SectionChanged;

    /// <summary>
    /// Switches the active section, optionally carrying a <paramref name="parameter"/> for the
    /// destination (issue #53 - e.g. which task's id <see cref="AppSection.DetalhesTarefa"/>
    /// should load). Existing no-parameter call sites keep working unchanged, since
    /// <paramref name="parameter"/> defaults to <see langword="null"/>.
    ///
    /// Unlike the original section-only behavior, navigating to the *same* section with a
    /// *different* parameter still raises <see cref="SectionChanged"/> - e.g. opening Detalhes for
    /// a second task while already viewing Detalhes for a first one must still swap the resolved
    /// view/reload data, not be silently ignored by a same-section guard.
    /// </summary>
    void NavigateTo(AppSection section, object? parameter = null);
}
