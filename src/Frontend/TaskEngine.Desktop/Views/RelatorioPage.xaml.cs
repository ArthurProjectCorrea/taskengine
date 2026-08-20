using TaskEngine.Desktop.ViewModels;

namespace TaskEngine.Desktop.Views;

/// <summary>
/// Relatório screen (plan item 9, RF-010/RF-011, ERS-Tarefas.md): the per-task activity timeline
/// (<see cref="RelatorioViewModel"/>) plus a CSV export. Code-behind is pure MAUI wiring -
/// binding-context assignment only, no timer (unlike <see cref="DetalhesTarefaPage"/>): a
/// concluded task's data is immutable (RN-006), so there is nothing here that needs to tick.
///
/// Like <see cref="DetalhesTarefaPage"/>, <see cref="RelatorioViewModel.ApplyParameter"/> (not
/// <c>Loaded</c>) drives the data load - called by <c>Navigation.SectionViewFactory</c> right
/// after this page is resolved (<c>INavigationAware</c> hook), before it is placed in the shell's
/// content area.
/// </summary>
public partial class RelatorioPage : ContentView
{
    public RelatorioPage(RelatorioViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
