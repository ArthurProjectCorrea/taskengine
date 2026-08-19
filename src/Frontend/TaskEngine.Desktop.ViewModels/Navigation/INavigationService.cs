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

    event Action<AppSection>? SectionChanged;

    void NavigateTo(AppSection section);
}
