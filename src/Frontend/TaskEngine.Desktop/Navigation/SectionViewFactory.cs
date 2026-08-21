using Microsoft.Extensions.DependencyInjection;
using TaskEngine.Desktop.ViewModels.Navigation;
using TaskEngine.Desktop.Views;

namespace TaskEngine.Desktop.Navigation;

/// <summary>
/// Resolves the View that represents each <see cref="AppSection"/> in the shell's content area.
/// Every section - Dashboard (issue #19), Tarefas (ERS-Tarefas.md RF-001/RF-003), Configuracoes
/// (issue #52, ERS-Configuracoes.md), DetalhesTarefa (issue #53) and Relatorio (plan item 9) - is
/// wired to its real screen.
/// </summary>
public sealed class SectionViewFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Takes <see cref="IServiceProvider"/> (same composition-root pattern as <c>App.CreateWindow</c>)
    /// instead of the concrete section Views, so each navigation resolves a fresh transient
    /// instance (page + its view model).
    /// </summary>
    public SectionViewFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Resolves <paramref name="section"/>'s View and, if its resolved <c>BindingContext</c> opts
    /// into <see cref="INavigationAware"/>, hands it <paramref name="parameter"/> (issue #53) -
    /// e.g. <c>DetalhesTarefaViewModel</c> receiving the task id to load. Every other section's
    /// view model is unaffected, since none of them implement that interface.
    /// </summary>
    public View CreateView(AppSection section, object? parameter = null)
    {
        View view = section switch
        {
            AppSection.Dashboard => _serviceProvider.GetRequiredService<DashboardPage>(),
            AppSection.Tarefas => _serviceProvider.GetRequiredService<TarefasPage>(),
            AppSection.Configuracoes => _serviceProvider.GetRequiredService<ConfiguracoesPage>(),
            AppSection.DetalhesTarefa => _serviceProvider.GetRequiredService<DetalhesTarefaPage>(),
            AppSection.Relatorio => _serviceProvider.GetRequiredService<RelatorioPage>(),
            _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown app section."),
        };

        if (view.BindingContext is INavigationAware navigationAware)
        {
            navigationAware.ApplyParameter(parameter);
        }

        return view;
    }
}
