using TaskEngine.Desktop.ViewModels;

namespace TaskEngine.Desktop.Views;

/// <summary>
/// Dashboard screen (issue #19): KPI cards, the "em andamento e a fazer" list and the selected
/// task's detail panel (timer + human/AI/expediente/não-mapeado proportions). Code-behind is kept
/// to MAUI-specific wiring only - the ticking "tempo de execução" timer needs
/// <see cref="IDispatcherTimer"/> (a MAUI type), which is why it is driven from here instead of
/// from <see cref="DashboardViewModel"/> itself (kept MAUI-free/testable - see its own
/// <see cref="DashboardViewModel.Tick"/> doc comment). The timer starts/stops on
/// <see cref="Loaded"/>/<see cref="Unloaded"/> so it does not keep ticking (and consuming CPU)
/// once the user navigates away to another section.
/// </summary>
public partial class DashboardPage : ContentView
{
    private readonly DashboardViewModel _viewModel;
    private readonly IDispatcherTimer _timer;

    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => _viewModel.Tick();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _timer.Start();
        _ = _viewModel.LoadAsync();
    }

    private void OnUnloaded(object? sender, EventArgs e) => _timer.Stop();
}
