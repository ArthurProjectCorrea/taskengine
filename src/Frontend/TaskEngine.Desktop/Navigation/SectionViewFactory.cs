using Microsoft.Extensions.DependencyInjection;
using TaskEngine.Desktop.ViewModels.Navigation;
using TaskEngine.Desktop.Views;

namespace TaskEngine.Desktop.Navigation;

/// <summary>
/// Resolves the View that represents each <see cref="AppSection"/> in the shell's content area.
/// Dashboard (issue #19) is the first section wired to its real screen; Tarefas/Configurações
/// still resolve to a titled <see cref="PlaceholderView"/> until their own Fase E work lands -
/// wiring them in later only means replacing the corresponding branch here, without touching
/// <see cref="ViewModels.ShellViewModel"/> or <see cref="NavigationService"/>.
/// </summary>
public sealed class SectionViewFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Takes <see cref="IServiceProvider"/> (same composition-root pattern as <c>App.CreateWindow</c>)
    /// instead of the concrete section Views, so each navigation resolves a fresh transient
    /// instance (page + its view model) - matching how <c>Tarefas</c>/<c>Configuracoes</c> already
    /// get a fresh <see cref="PlaceholderView"/> per navigation today.
    /// </summary>
    public SectionViewFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public View CreateView(AppSection section) => section switch
    {
        AppSection.Dashboard => _serviceProvider.GetRequiredService<DashboardPage>(),
        AppSection.Tarefas => new PlaceholderView("Tarefas (placeholder)"),
        AppSection.Configuracoes => new PlaceholderView("Configurações (placeholder)"),
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown app section."),
    };
}
