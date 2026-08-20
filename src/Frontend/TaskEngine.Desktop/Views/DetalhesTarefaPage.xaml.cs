using TaskEngine.Desktop.ViewModels;

namespace TaskEngine.Desktop.Views;

/// <summary>
/// Detalhes da Tarefa screen (issue #53, ERS-Tarefas.md): the Dashboard's progress panel (timer +
/// human/AI/expediente/não-mapeado) for a single fixed task, plus its full "Registros de tempo" and
/// a "Voltar"/"Relatório" action. Code-behind is kept to MAUI-specific wiring only - same
/// ticking-timer rationale as <see cref="DashboardPage"/> (the live "tempo de execução" timer needs
/// <see cref="IDispatcherTimer"/>, a MAUI type, so it is driven from here rather than from
/// <see cref="DetalhesTarefaViewModel"/> itself).
///
/// Unlike <see cref="DashboardPage"/>/<see cref="TarefasPage"/>, <see cref="Loaded"/> does not
/// trigger the data load here - that already happens via
/// <see cref="DetalhesTarefaViewModel.ApplyParameter"/>, called by
/// <c>Navigation.SectionViewFactory</c> right after this page is resolved (issue #53's
/// <c>INavigationAware</c> hook), before this page is even placed in the shell's content area.
/// </summary>
public partial class DetalhesTarefaPage : ContentView
{
    private readonly DetalhesTarefaViewModel _viewModel;
    private readonly IDispatcherTimer _timer;

    public DetalhesTarefaPage(DetalhesTarefaViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => _viewModel.Tick();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e) => _timer.Start();

    private void OnUnloaded(object? sender, EventArgs e) => _timer.Stop();
}
