using TaskEngine.Desktop.ViewModels;

namespace TaskEngine.Desktop.Views;

/// <summary>
/// Code-behind kept to initialization only, per project convention: the view model is resolved
/// by the MAUI DI container (registered in <c>MauiProgram.cs</c>) and injected directly into this
/// constructor, then set as <see cref="BindableObject.BindingContext"/>. No business logic here.
/// </summary>
public partial class OnboardingPage : ContentPage
{
    public OnboardingPage(OnboardingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
