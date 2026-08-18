using TaskEngine.Desktop.ViewModels;

namespace TaskEngine.Desktop.Views;

/// <summary>
/// Code-behind kept to initialization only, per project convention: the view model is resolved by
/// the MAUI DI container (registered in <c>MauiProgram.cs</c>) and injected directly into this
/// constructor, then set as <see cref="BindableObject.BindingContext"/>.
/// </summary>
public partial class CreateTaskPage : ContentPage
{
    private readonly CreateTaskViewModel _viewModel;

    public CreateTaskPage(CreateTaskViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        Loaded += OnLoaded;
    }

    /// <summary>
    /// MAUI has no async constructor, so the schema load (I/O-bound: a cache read, and possibly a
    /// live provider call) is kicked off here instead, once the page actually reaches the screen.
    /// Unsubscribes immediately - this only needs to run once per page instance.
    /// </summary>
    private async void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeAsync(CancellationToken.None);
    }
}
