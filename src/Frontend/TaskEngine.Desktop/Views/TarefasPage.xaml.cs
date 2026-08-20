using TaskEngine.Desktop.ViewModels;

namespace TaskEngine.Desktop.Views;

/// <summary>
/// Tarefas (list) screen (ERS-Tarefas.md RF-001/RF-003): search, sync, paginated list of every
/// task with its status/action. Code-behind is kept to the minimum MAUI-specific wiring - loading
/// on <see cref="Loaded"/> only, no timers/tickers needed here (unlike <c>DashboardPage</c>, this
/// screen shows a static "tempo investido" per row rather than a live-ticking selected-task timer).
/// </summary>
public partial class TarefasPage : ContentView
{
    private readonly TarefasViewModel _viewModel;

    public TarefasPage(TarefasViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, EventArgs e) => _ = _viewModel.LoadAsync();
}
