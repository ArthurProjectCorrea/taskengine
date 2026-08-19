using TaskEngine.Desktop.Navigation;
using TaskEngine.Desktop.ViewModels;
using TaskEngine.Desktop.ViewModels.Navigation;

namespace TaskEngine.Desktop.Views;

/// <summary>
/// Root shell page (issue #2): hosts the 56px sidebar plus a content area that swaps between
/// sections. Code-behind is kept to wiring only - which section is active lives in
/// <see cref="ShellViewModel"/> (bound in XAML), and which View represents each section is
/// resolved by <see cref="SectionViewFactory"/> (a Desktop-only wiring concern kept out of the view
/// model so it stays MAUI-free/testable). Resolved as a singleton (see
/// <c>MauiProgram.RegisterPresentation</c>) - this page is created once and only ever hidden, never
/// destroyed, matching <c>MainWindowManager</c>'s hide-to-tray window.
/// </summary>
public partial class MainShellPage : ContentPage
{
    private readonly INavigationService _navigationService;
    private readonly SectionViewFactory _sectionViewFactory;

    public MainShellPage(ShellViewModel viewModel, INavigationService navigationService, SectionViewFactory sectionViewFactory)
    {
        InitializeComponent();
        BindingContext = viewModel;

        _navigationService = navigationService;
        _sectionViewFactory = sectionViewFactory;
        _navigationService.SectionChanged += OnSectionChanged;

        ShowSection(_navigationService.CurrentSection);
    }

    private void OnSectionChanged(AppSection section) => ShowSection(section);

    private void ShowSection(AppSection section) => SectionHost.Content = _sectionViewFactory.CreateView(section);
}
