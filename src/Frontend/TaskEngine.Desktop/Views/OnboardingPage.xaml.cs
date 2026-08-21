using System.ComponentModel;
using TaskEngine.Desktop.ViewModels;

namespace TaskEngine.Desktop.Views;

/// <summary>
/// Code-behind kept close to initialization, per project convention: the view model is resolved
/// by the MAUI DI container (registered in <c>MauiProgram.cs</c>) and injected directly into this
/// constructor, then set as <see cref="BindableObject.BindingContext"/>. The one bit of navigation
/// logic here (swapping the window's root page to <see cref="MainShellPage"/> once a provider
/// connects) has nowhere else to live: <c>OnboardingViewModel</c> intentionally has no MAUI/page
/// dependency, and <c>App.xaml.cs</c> only decides the *startup* page, not what happens mid-session.
/// </summary>
public partial class OnboardingPage : ContentPage
{
    private readonly OnboardingViewModel _viewModel;
    private readonly MainShellPage _mainShellPage;

    public OnboardingPage(OnboardingViewModel viewModel, MainShellPage mainShellPage)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _mainShellPage = mainShellPage;
        BindingContext = viewModel;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>
    /// Antes desta correção, um provedor recém-conectado só levava ao <see cref="MainShellPage"/>
    /// no próximo start do app (decisão de escopo deliberada da issue #2, nunca revisitada) — na
    /// prática, a tela de sucesso ficava presa sem avançar, mesmo com a conexão funcionando.
    /// Troca a página raiz da janela atual assim que <see cref="OnboardingViewModel.State"/> vira
    /// <see cref="ConnectionState.Connected"/>, sem recriar a janela nativa (preserva posição,
    /// tamanho e o comportamento de bandeja já configurados em <c>MainWindowManager</c>).
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OnboardingViewModel.State) || _viewModel.State != ConnectionState.Connected)
        {
            return;
        }

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        var app = Microsoft.Maui.Controls.Application.Current;
        var window = app?.Windows.Count > 0 ? app.Windows[0] : null;
        if (window is not null)
        {
            window.Page = _mainShellPage;
        }
    }
}
