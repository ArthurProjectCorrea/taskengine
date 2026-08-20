using TaskEngine.Desktop.ViewModels.Navigation;

namespace TaskEngine.Desktop.Navigation;

/// <summary>
/// Concrete, MAUI-free implementation of <see cref="INavigationService"/>: just tracks which
/// <see cref="AppSection"/> (and, since issue #53, which parameter - e.g. a task id) is active,
/// and raises <see cref="SectionChanged"/> so listeners (today:
/// <see cref="TaskEngine.Desktop.ViewModels.ShellViewModel"/> and
/// <see cref="Views.MainShellPage"/>) can react. Registered as a singleton (see
/// <c>MauiProgram.RegisterPresentation</c>) - there is exactly one shell/window for the app's
/// lifetime (it is only ever hidden, never recreated - see <c>MainWindowManager</c>), so there is
/// nothing to reset between resolutions.
/// </summary>
internal sealed class NavigationService : INavigationService
{
    public AppSection CurrentSection { get; private set; } = AppSection.Dashboard;

    public object? CurrentParameter { get; private set; }

    public event Action<AppSection>? SectionChanged;

    public void NavigateTo(AppSection section, object? parameter = null)
    {
        // Guards on section AND parameter (not just section, as before issue #53) - navigating to
        // the same section with a different parameter (e.g. Detalhes for a different task) must
        // still swap the resolved view/reload data.
        if (CurrentSection == section && Equals(CurrentParameter, parameter))
        {
            return;
        }

        CurrentSection = section;
        CurrentParameter = parameter;
        SectionChanged?.Invoke(section);
    }
}
