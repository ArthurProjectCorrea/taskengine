using TaskEngine.Desktop.ViewModels;

namespace TaskEngine.Desktop.Views;

/// <summary>
/// Configurações screen (issue #52, ERS-Configuracoes.md): expediente, provedores, backup,
/// exportação CSV geral e "Sobre". Code-behind is kept to the minimum MAUI-specific wiring -
/// loading on <see cref="Loaded"/> only, same convention as <see cref="TarefasPage"/> (no timers -
/// unlike <see cref="DashboardPage"/>, nothing on this screen ticks live).
/// </summary>
public partial class ConfiguracoesPage : ContentView
{
    private readonly ConfiguracoesViewModel _viewModel;

    public ConfiguracoesPage(ConfiguracoesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, EventArgs e) => _ = _viewModel.LoadAsync();
}
