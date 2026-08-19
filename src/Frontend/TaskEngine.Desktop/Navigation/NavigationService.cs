using TaskEngine.Desktop.ViewModels.Navigation;

namespace TaskEngine.Desktop.Navigation;

/// <summary>
/// Concrete, MAUI-free implementation of <see cref="INavigationService"/>: just tracks which
/// <see cref="AppSection"/> is active and raises <see cref="SectionChanged"/> so listeners (today:
/// <see cref="TaskEngine.Desktop.ViewModels.ShellViewModel"/> and
/// <see cref="Views.MainShellPage"/>) can react. Registered as a singleton (see
/// <c>MauiProgram.RegisterPresentation</c>) - there is exactly one shell/window for the app's
/// lifetime (it is only ever hidden, never recreated - see <c>MainWindowManager</c>), so there is
/// nothing to reset between resolutions.
/// </summary>
internal sealed class NavigationService : INavigationService
{
    public AppSection CurrentSection { get; private set; } = AppSection.Dashboard;

    public event Action<AppSection>? SectionChanged;

    public void NavigateTo(AppSection section)
    {
        if (CurrentSection == section)
        {
            return;
        }

        CurrentSection = section;
        SectionChanged?.Invoke(section);
    }
}
