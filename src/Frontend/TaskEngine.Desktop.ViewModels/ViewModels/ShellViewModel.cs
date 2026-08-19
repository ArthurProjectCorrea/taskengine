using TaskEngine.Desktop.Mvvm;
using TaskEngine.Desktop.ViewModels.Navigation;

namespace TaskEngine.Desktop.ViewModels;

/// <summary>
/// View model for the shell's 56px sidebar (issue #2): exposes the currently active
/// <see cref="AppSection"/> and one command per sidebar icon (Dashboard/Tarefas/Configurações).
/// Contains no view-resolution logic - deciding which View represents a given section is a
/// Desktop-only wiring concern (see <c>TaskEngine.Desktop.Navigation.SectionViewFactory</c>), kept
/// out of here so this view model stays MAUI-free and testable, matching the rest of this project.
/// </summary>
public sealed class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private AppSection _currentSection;

    public ShellViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        _currentSection = navigationService.CurrentSection;
        _navigationService.SectionChanged += OnSectionChanged;

        NavigateToDashboardCommand = new RelayCommand(_ => _navigationService.NavigateTo(AppSection.Dashboard));
        NavigateToTarefasCommand = new RelayCommand(_ => _navigationService.NavigateTo(AppSection.Tarefas));
        NavigateToConfiguracoesCommand = new RelayCommand(_ => _navigationService.NavigateTo(AppSection.Configuracoes));
    }

    public AppSection CurrentSection
    {
        get => _currentSection;
        private set => SetProperty(ref _currentSection, value);
    }

    public RelayCommand NavigateToDashboardCommand { get; }

    public RelayCommand NavigateToTarefasCommand { get; }

    public RelayCommand NavigateToConfiguracoesCommand { get; }

    private void OnSectionChanged(AppSection section) => CurrentSection = section;
}
