using TaskEngine.Desktop.ViewModels.Navigation;
using TaskEngine.Desktop.Views;

namespace TaskEngine.Desktop.Navigation;

/// <summary>
/// Resolves the View that represents each <see cref="AppSection"/> in the shell's content area.
/// Today every section resolves to a titled <see cref="PlaceholderView"/> - the real screens
/// (Dashboard, Tarefas, Configurações) are Fase E work; wiring them in later only means replacing
/// the corresponding branch here, without touching <see cref="ViewModels.ShellViewModel"/> or
/// <see cref="NavigationService"/>.
/// </summary>
public sealed class SectionViewFactory
{
    public View CreateView(AppSection section) => section switch
    {
        AppSection.Dashboard => new PlaceholderView("Dashboard (placeholder)"),
        AppSection.Tarefas => new PlaceholderView("Tarefas (placeholder)"),
        AppSection.Configuracoes => new PlaceholderView("Configurações (placeholder)"),
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown app section."),
    };
}
